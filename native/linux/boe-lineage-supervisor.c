#define _GNU_SOURCE
#include <ctype.h>
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <limits.h>
#include <linux/magic.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/pidfd.h>
#include <sys/prctl.h>
#include <sys/stat.h>
#include <sys/vfs.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

/* T041-FALLBACK-PROC prototype. One single-thread/exclusive reaper.
 * Proc is a discovery worklist; only sealed launch plus ECHILD proves retirement
 * within the declared ordinary same-PID-namespace lineage. No production wiring. */
static const char *run_id, *reason = "ready";
static bool uncertain, stopping, launched;
static bool status_usable = true, exec_pending;
static bool clock_failed;
static int root_fd = -1, exec_fd = -1, root_exit = -1, root_signal, last_errno;
static pid_t root_pid = -1;
static int grace_ms, deadline_ms;
static long long stop_at;
static volatile sig_atomic_t signal_stop;
static unsigned char exec_error[sizeof(int)];
static size_t exec_bytes;
static DIR *proc_directory;
static int namespace_fd = -1;
static struct stat namespace_identity;
#define CHILD_LIMIT 128
struct child { pid_t pid; int fd; bool term_sent, kill_sent; };
static struct child children[CHILD_LIMIT];
static size_t child_count;

/* Only PID/PPID are consumed; comm and all other stat fields are discarded. */
static bool parse_stat(const char *text, pid_t expected, pid_t *parent) {
    char *end; errno = 0;
    long pid = strtol(text, &end, 10);
    if (errno || pid != expected || end == text || end[0] != ' ' || end[1] != '(') return false;
    const char *delimiter = strrchr(end + 1, ')');
    if (!delimiter || strlen(delimiter) < 5 || delimiter[1] != ' ' || !isalpha((unsigned char)delimiter[2]) || delimiter[3] != ' ') return false;
    const char *start = delimiter + 4;
    if (!isdigit((unsigned char)*start)) return false;
    errno = 0; long ppid = strtol(start, &end, 10);
    if (errno || end == start || *end != ' ' || ppid < 0 || ppid > INT_MAX) return false;
    *parent = (pid_t)ppid; return true;
}
static int read_proc(const char *relative, char *buffer, size_t capacity) {
    int fd = openat(dirfd(proc_directory), relative, O_RDONLY | O_CLOEXEC);
    if (fd < 0) return -1;
    size_t used = 0;
    while (used < capacity - 1) {
        ssize_t n = read(fd, buffer + used, capacity - 1 - used);
        if (n == 0) { buffer[used] = 0; close(fd); return 0; }
        if (n < 0) { if (errno == EINTR) continue; int e = errno; close(fd); errno = e; return -1; }
        used += (size_t)n;
    }
    close(fd); errno = EOVERFLOW; return -1;
}
static bool proc_ready(void) {
    int fd = open("/proc", O_RDONLY | O_DIRECTORY | O_CLOEXEC);
    if (fd < 0) return false;
    struct statfs fs;
    if (fstatfs(fd, &fs)) { int e = errno; close(fd); errno = e; return false; }
    if (fs.f_type != PROC_SUPER_MAGIC) { close(fd); errno = ENOTSUP; return false; }
    proc_directory = fdopendir(fd); if (!proc_directory) { close(fd); return false; }
    char stat_text[4096], status[16384], self[32]; pid_t parent;
    if (read_proc("self/stat", stat_text, sizeof stat_text)) return false;
    if (!parse_stat(stat_text, getpid(), &parent) || parent != getppid()) { errno = EPROTO; return false; }
    ssize_t size = readlinkat(fd, "self", self, sizeof self - 1);
    if (size < 1 || size >= (ssize_t)sizeof self - 1) return false;
    self[size] = 0; char *end; errno = 0; long pid = strtol(self, &end, 10);
    if (errno || *end || pid != getpid()) return false;
    if (read_proc("self/status", status, sizeof status)) return false;
    const char *nspid = strstr(status, "\nNSpid:");
    if (!nspid) { errno = ENOTSUP; return false; }
    errno = 0; pid = strtol(nspid + 7, &end, 10);
    if (errno || pid != getpid()) return false;
    while (*end == ' ' || *end == '\t') end++;
    if (*end != '\n') { errno = ENOTSUP; return false; } /* No ancestor proc PID view. */
    namespace_fd = openat(fd, "self/ns/pid", O_RDONLY | O_CLOEXEC);
    return namespace_fd >= 0 && fstat(namespace_fd, &namespace_identity) == 0;
}

static long long now_ms(void) {
    struct timespec t; if (clock_gettime(CLOCK_MONOTONIC, &t)) { clock_failed = true; return 0; }
    return (long long)t.tv_sec * 1000 + t.tv_nsec / 1000000;
}
static void seal(const char *why) {
    if (!stopping) { stopping = true; reason = why; stop_at = now_ms(); }
}
static void lose(const char *why, int error) {
    if (!uncertain) { seal(why); uncertain = true; reason = why; last_errno = error; }
}
static size_t find_child(pid_t pid) {
    for (size_t i = 0; i < child_count; i++) if (children[i].pid == pid) return i;
    return child_count;
}
static bool child_namespace(pid_t pid) {
    char path[64]; snprintf(path, sizeof path, "%d/ns/pid", pid);
    int fd = openat(dirfd(proc_directory), path, O_RDONLY | O_CLOEXEC);
    if (fd >= 0) {
        struct stat identity; int result = fstat(fd, &identity), error = errno; close(fd);
        if (result) { lose("child-namespace", error); return false; }
        if (identity.st_dev == namespace_identity.st_dev && identity.st_ino == namespace_identity.st_ino) return true;
        lose("scope-breach", 0); return false;
    }
    int error = errno;
    if (error == ENOENT || error == ESRCH) {
        /* A zombie may have lost ns metadata. Observe, but do not reap before
         * pidfd binding. Exclusive reaping keeps this direct-child PID stable. */
        siginfo_t info = {0};
        if (waitid(P_PID, (id_t)pid, &info, WEXITED | WNOHANG | WNOWAIT | __WALL) == 0 && info.si_pid == pid) return true;
    }
    lose("child-namespace", error); return false;
}
static void discover_children(void) {
    if (!launched) return;
    /* Bound every directory slice, not just matches. Retain the cursor between
     * turns; no snapshot, including an empty one, has terminal significance. */
    for (int slice = 0; slice < CHILD_LIMIT; slice++) {
        errno = 0; struct dirent *entry = readdir(proc_directory);
        if (!entry) {
            int error = errno;
            if (error) lose("proc-directory", error);
            rewinddir(proc_directory); return;
        }
        if (!isdigit((unsigned char)entry->d_name[0])) continue;
        char *end; errno = 0; long value = strtol(entry->d_name, &end, 10);
        if (errno || *end || value < 1 || value > INT_MAX) continue;
        pid_t pid = (pid_t)value;
        if (find_child(pid) < child_count) continue; /* Held unreaped incarnation. */
        char path[64], text[4096]; pid_t parent;
        snprintf(path, sizeof path, "%d/stat", pid);
        if (read_proc(path, text, sizeof text)) {
            int error = errno;
            if (error != ENOENT && error != ESRCH && error != EACCES && error != EPERM) lose("proc-stat", error);
            continue; /* Unclassified metadata never grants signal authority. */
        }
        if (!parse_stat(text, pid, &parent)) { lose("proc-stat-format", EPROTO); continue; }
        if (parent != getpid()) continue;
        if (child_count == CHILD_LIMIT) { lose("child-capacity", ENOSPC); continue; }
        (void)child_namespace(pid); /* Failure latches uncertainty; still clean up. */
        int fd = pidfd_open(pid, 0);
        if (fd < 0) lose("child-pidfd", errno);
        /* No wait occurred after PPID verification. Even a failed bind retains
         * the unreaped incarnation so it is not retried/signalled numerically. */
        children[child_count++] = (struct child){ .pid = pid, .fd = fd };
    }
}
static void emit(const char *state, bool complete) {
    if (!status_usable) return;
    char line[1024];
    int n = snprintf(line, sizeof line,
        "{\"version\":1,\"runId\":\"%s\",\"backend\":\"native-lineage\",\"guarantee\":\"ordinary-same-namespace-lineage\",\"state\":\"%s\",\"reason\":\"%s\",\"cleanupComplete\":%s,\"authorityRetained\":%s,\"rootExitCode\":%d,\"rootSignal\":%d,\"errno\":%d}\n",
        run_id, state, reason, complete ? "true" : "false", complete ? "false" : "true", root_exit, root_signal, last_errno);
    ssize_t written;
    do { written = write(STDOUT_FILENO, line, (size_t)n); } while (written < 0 && errno == EINTR);
    if (written != n) { int error = errno; status_usable = false; lose("status-unavailable", error); }
}
static void on_signal(int sig) { (void)sig; signal_stop = 1; }
static int parse_ms(const char *arg) {
    char *end; errno = 0; long n = strtol(arg, &end, 10);
    return errno || !*arg || *end || n < 1 || n > 30000 ? -1 : (int)n;
}
static void child_error(int fd, int error) {
    ssize_t n; do { n = write(fd, &error, sizeof error); } while (n < 0 && errno == EINTR);
    (void)n; _exit(126);
}
static void launch(char **argv) {
    int gate[2], error[2];
    if (pipe2(gate, O_CLOEXEC)) { lose("bootstrap-pipe", errno); return; }
    if (pipe2(error, O_CLOEXEC)) { int e = errno; close(gate[0]); close(gate[1]); lose("bootstrap-pipe", e); return; }
    pid_t p = fork();
    if (p < 0) { int e = errno; close(gate[0]); close(gate[1]); close(error[0]); close(error[1]); lose("fork-failed", e); return; }
    if (!p) {
        close(gate[1]); close(error[0]);
        struct sigaction sa = { .sa_handler = SIG_DFL }; sigemptyset(&sa.sa_mask);
        if (sigaction(SIGTERM, &sa, NULL) || sigaction(SIGINT, &sa, NULL) || sigaction(SIGPIPE, &sa, NULL) || sigaction(SIGCHLD, &sa, NULL)) child_error(error[1], errno);
        sigset_t mask; sigemptyset(&mask); if (sigprocmask(SIG_SETMASK, &mask, NULL)) child_error(error[1], errno);
        int null = open("/dev/null", O_RDONLY | O_CLOEXEC);
        if (null < 0 || dup2(null, STDIN_FILENO) < 0 || dup2(STDERR_FILENO, STDOUT_FILENO) < 0) child_error(error[1], errno);
        close(null);
        char c; ssize_t n; do { n = read(gate[0], &c, 1); } while (n < 0 && errno == EINTR);
        close(gate[0]); if (n != 1 || c != 'L') _exit(125);
        if (setpgid(0, 0)) child_error(error[1], errno);
        execv(argv[0], argv); child_error(error[1], errno);
    }
    launched = true; root_pid = p; close(gate[0]); close(error[1]);
    root_fd = pidfd_open(p, 0); /* p is still our unreaped direct child. */
    children[child_count++] = (struct child){ .pid = p, .fd = root_fd };
    if (root_fd < 0) { lose("root-pidfd", errno); close(gate[1]); close(error[0]); return; }
    exec_fd = error[0]; exec_pending = true;
    if (fcntl(exec_fd, F_SETFL, O_NONBLOCK)) { lose("exec-channel", errno); close(gate[1]); return; }
    if (write(gate[1], "L", 1) != 1) lose("bootstrap-release", errno);
    close(gate[1]);
}
static void observe_exec(void) {
    if (!exec_pending) return;
    ssize_t n = read(exec_fd, exec_error + exec_bytes, sizeof(int) - exec_bytes);
    if (n > 0) {
        exec_bytes += (size_t)n;
        if (exec_bytes < sizeof(int)) return;
        int error; memcpy(&error, exec_error, sizeof error); lose("exec-failed", error);
    } else if (n < 0) {
        if (errno == EINTR || errno == EAGAIN) return;
        lose("exec-channel", errno);
    } else if (exec_bytes) lose("exec-channel", EIO);
    else if (!stopping) { reason = "started"; emit("Started", false); }
    exec_pending = false; close(exec_fd); exec_fd = -1;
}
static void signal_children(void) {
    bool force = now_ms() - stop_at >= grace_ms || clock_failed;
    for (size_t i = 0; i < child_count; i++) {
        struct child *child = &children[i];
        if (!child->term_sent) {
            if (child->fd >= 0 && pidfd_send_signal(child->fd, SIGTERM, NULL, 0) && errno != ESRCH) lose("signal-failed", errno);
            child->term_sent = true;
        }
        if (force && !child->kill_sent) {
            if (child->fd >= 0 && pidfd_send_signal(child->fd, SIGKILL, NULL, 0) && errno != ESRCH) lose("signal-failed", errno);
            child->kill_sent = true;
        }
    }
}
static bool reap(void) {
    int status; pid_t p = 0;
    for (int slice = 0; slice < CHILD_LIMIT; slice++) {
        p = waitpid(-1, &status, __WALL | WNOHANG);
        if (p <= 0) break;
        if (!WIFEXITED(status) && !WIFSIGNALED(status)) continue;
        size_t found = find_child(p);
        if (found < child_count) {
            if (children[found].fd >= 0) close(children[found].fd);
            children[found] = children[--child_count];
        }
        if (p == root_pid) {
            root_exit = WIFEXITED(status) ? WEXITSTATUS(status) : -1;
            root_signal = WIFSIGNALED(status) ? WTERMSIG(status) : 0;
            root_pid = -1; root_fd = -1; /* Never alias a later reused PID. */
            seal("root-exited");
        }
    }
    if (p < 0 && errno == ECHILD) {
        if (child_count) lose("child-inventory", ECHILD);
        return true;
    }
    if (p < 0 && errno != EINTR) lose("wait-failed", errno);
    return false;
}
int main(int argc, char **argv) {
    if (argc < 5 || strlen(argv[1]) < 1 || strlen(argv[1]) > 64) return 64;
    for (const char *c = argv[1]; *c; c++) if (!((*c >= 'A' && *c <= 'Z') || (*c >= 'a' && *c <= 'z') || (*c >= '0' && *c <= '9') || *c == '-')) return 64;
    run_id = argv[1]; grace_ms = parse_ms(argv[2]); deadline_ms = parse_ms(argv[3]);
    if (grace_ms < 0 || deadline_ms < 0) return 64;
    alarm(0); /* A caller/bootstrap alarm may survive exec; never inherit that deadline. */
    struct sigaction sa = { .sa_handler = SIG_DFL }; sigemptyset(&sa.sa_mask);
    if (sigaction(SIGCHLD, &sa, NULL)) return 70;
    sa.sa_handler = SIG_IGN; if (sigaction(SIGPIPE, &sa, NULL)) return 70;
    sa.sa_handler = on_signal; if (sigaction(SIGTERM, &sa, NULL) || sigaction(SIGINT, &sa, NULL)) return 70;
    sigset_t mask; sigemptyset(&mask); if (sigprocmask(SIG_SETMASK, &mask, NULL)) return 70;
    if (fcntl(STDIN_FILENO, F_SETFL, O_NONBLOCK) || fcntl(STDOUT_FILENO, F_SETFL, O_NONBLOCK)) return 70;
    int enabled = 0;
    if (close_range(3, UINT_MAX, 0) || prctl(PR_SET_CHILD_SUBREAPER, 1) || prctl(PR_GET_CHILD_SUBREAPER, &enabled) || enabled != 1) { lose("prerequisite-unavailable", errno); emit("Uncertain", true); return 2; }
    int self = pidfd_open(getpid(), 0);
    if (self < 0 || pidfd_send_signal(self, 0, NULL, 0)) { lose("pidfd-unavailable", errno); emit("Uncertain", true); return 2; }
    close(self);
    if (!proc_ready()) { lose("proc-unavailable", errno); emit("Uncertain", true); return 2; }
    (void)now_ms();
    if (clock_failed) { lose("clock-unavailable", errno); emit("Uncertain", true); return 2; }
    emit("Ready", false);
    bool stop_reported = false, uncertainty_reported = false;
    for (;;) {
        (void)now_ms(); if (clock_failed) lose("clock-unavailable", errno);
        if (signal_stop) seal("helper-signal");
        char commands[32]; ssize_t n = read(STDIN_FILENO, commands, sizeof commands);
        if (n == 0) lose("owner-lost", 0);
        else if (n < 0 && errno != EINTR && errno != EAGAIN) lose("owner-channel", errno);
        else if (n > 0) for (ssize_t i = 0; i < n; i++) {
            if (commands[i] == 'L' && !launched && !stopping) launch(&argv[4]);
            else if (commands[i] == 'S') seal("stop-requested");
            else if (commands[i] == 'C') seal("cancelled");
            else if (commands[i] == 'U') lose("scope-breach", 0);
            else lose("invalid-command", 0);
        }
        observe_exec();
        discover_children(); /* Always bind before the next sole-reaper turn. */
        if (stopping) {
            if (!stop_reported) { emit("Stopping", false); stop_reported = true; }
            signal_children();
            if (now_ms() - stop_at >= deadline_ms) lose("stop-timeout", 0);
            if (uncertain && !uncertainty_reported) { emit("Uncertain", false); uncertainty_reported = true; }
        }
        bool empty = reap();
        if (stopping && empty && !exec_pending) {
            emit(uncertain ? "Uncertain" : "StoppedWithinScope", true);
            return uncertain ? 2 : 0;
        }
        struct pollfd input = { .fd = STDIN_FILENO, .events = POLLIN };
        /* EOF stays readable, so avoid spinning after owner loss. */
        poll(stopping ? NULL : &input, stopping ? 0 : 1, 10);
    }
}
