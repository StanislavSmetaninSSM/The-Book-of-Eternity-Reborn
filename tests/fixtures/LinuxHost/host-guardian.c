#define _GNU_SOURCE
#include <dirent.h>
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/pidfd.h>
#include <sys/prctl.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

/* Test-only independent outer guardian. No production helper code is shared.
 * Only its own unreaped direct/adopted children confer signalling authority. */
struct owned { pid_t pid; int fd; bool killed; };
static struct owned children[64];
static size_t count;
static int emergency, failures, reaped;
static long long now(void) {
    struct timespec t;
    return clock_gettime(CLOCK_MONOTONIC, &t) ? -1 : (long long)t.tv_sec * 1000 + t.tv_nsec / 1000000;
}
static bool parent_of(pid_t expected, pid_t *parent) {
    char name[80], buffer[16384];
    if (snprintf(name, sizeof name, "/proc/%d/status", expected) >= (int)sizeof name) return false;
    int fd = open(name, O_RDONLY | O_CLOEXEC); if (fd < 0) return false;
    size_t used = 0;
    for (;;) {
        ssize_t n = read(fd, buffer + used, sizeof buffer - 1 - used);
        if (n < 0 && errno == EINTR) continue;
        if (n < 0 || used == sizeof buffer - 1) { close(fd); return false; }
        if (!n) break;
        used += (size_t)n;
    }
    close(fd); buffer[used] = 0;
    int pid_fields = 0, parent_fields = 0; long pid = -1, ppid = -1;
    char *save;
    for (char *line = strtok_r(buffer, "\n", &save); line; line = strtok_r(NULL, "\n", &save)) {
        char *end;
        if (!strncmp(line, "Pid:\t", 5)) {
            errno = 0; pid = strtol(line + 5, &end, 10);
            if (errno || *end) return false;
            pid_fields++;
        }
        if (!strncmp(line, "PPid:\t", 6)) {
            errno = 0; ppid = strtol(line + 6, &end, 10);
            if (errno || *end) return false;
            parent_fields++;
        }
    }
    if (pid_fields != 1 || parent_fields != 1 || pid != expected || ppid < 0 || ppid > 2147483647) return false;
    *parent = (pid_t)ppid; return true;
}
static bool bind_child(pid_t pid) {
    for (size_t i = 0; i < count; i++) if (children[i].pid == pid) return true;
    if (count == sizeof children / sizeof children[0]) { failures++; return false; }
    pid_t parent;
    if (!parent_of(pid, &parent) || parent != getpid()) return false;
    /* No reap occurs between verified own PPID and pidfd acquisition. */
    int fd = pidfd_open(pid, 0);
    if (fd < 0) { failures++; return false; }
    children[count++] = (struct owned){ .pid = pid, .fd = fd };
    return true;
}
static void discover(DIR *proc) {
    rewinddir(proc);
    for (;;) {
        errno = 0; struct dirent *entry = readdir(proc);
        if (!entry) { if (errno) failures++; return; }
        char *end; errno = 0; long pid = strtol(entry->d_name, &end, 10);
        if (errno || *end || pid <= 0 || pid > 2147483647) continue;
        (void)bind_child((pid_t)pid);
    }
}
static void kill_bound(struct owned *child) {
    if (child->killed) return;
    if (pidfd_send_signal(child->fd, SIGKILL, NULL, 0) && errno != ESRCH) { failures++; return; }
    child->killed = true; emergency++;
}
static void expire(void) { signal(SIGALRM, SIG_DFL); alarm(8); }
static int actor(const char *mode) {
    expire();
    if (!strcmp(mode, "normal")) return 0;
    int gate[2]; if (pipe2(gate, O_CLOEXEC)) return 91;
    pid_t helper = fork(); if (helper < 0) return 91;
    if (!helper) {
        expire(); close(gate[0]);
        int exec_gate[2]; if (pipe2(exec_gate, O_CLOEXEC)) _exit(91);
        pid_t host = fork(); if (host < 0) _exit(91);
        if (!host) {
            expire(); close(gate[1]); close(exec_gate[1]);
            if (!strcmp(mode, "held-gate")) { char c; ssize_t n = read(exec_gate[0], &c, 1); (void)n; _exit(0); }
            close(exec_gate[0]); for (;;) pause();
        }
        close(exec_gate[0]);
        if (write(gate[1], "r", 1) != 1) _exit(91);
        close(gate[1]);
        if (!strcmp(mode, "helper-loss")) _exit(0);
        for (;;) pause();
    }
    close(gate[1]); char ready;
    if (read(gate[0], &ready, 1) != 1) return 91;
    close(gate[0]);
    if (!strcmp(mode, "driver-loss")) return 0;
    if (!strcmp(mode, "helper-loss")) { int s; while (waitpid(helper, &s, 0) < 0 && errno == EINTR) {} return 0; }
    /* Deliberately keep the whole unreleased fixture topology alive until the
     * guardian deadline. No production launch or stop algorithm participates. */
    for (;;) pause();
}
int main(int argc, char **argv) {
    if (argc == 3 && !strcmp(argv[1], "--worker-marker")) {
        int fd = open(argv[2], O_WRONLY | O_CREAT | O_EXCL | O_CLOEXEC, 0600);
        if (fd < 0) return 71;
        close(fd); return 0;
    }
    if (argc == 2 && !strcmp(argv[1], "--sentinel")) { expire(); for (;;) pause(); }
    if (argc == 3 && !strcmp(argv[1], "--actor")) return actor(argv[2]);
    if (argc >= 3 && !strcmp(argv[1], "--expire-exec")) {
        expire(); execv(argv[2], &argv[2]); return 127;
    }
    if (argc < 5) return 64;
    signal(SIGCHLD, SIG_DFL); signal(SIGPIPE, SIG_IGN);
    sigset_t empty; sigemptyset(&empty);
    if (sigprocmask(SIG_SETMASK, &empty, NULL) || prctl(PR_SET_CHILD_SUBREAPER, 1)) return 78;
    pid_t parent; DIR *proc = opendir("/proc"); int self = pidfd_open(getpid(), 0);
    if (!proc || !parent_of(getpid(), &parent) || parent != getppid() || self < 0 || now() < 0) return 78;
    if (pidfd_send_signal(self, 0, NULL, 0)) return 78;
    close(self);
    char *end; long limit = strtol(argv[2], &end, 10);
    if (*end || limit < 50 || limit > 30000) return 64;
    FILE *report = fopen(argv[1], "we"); if (!report) return 78;
    int gate[2]; if (pipe2(gate, O_CLOEXEC)) return 78;
    pid_t driver = fork(); if (driver < 0) return 78;
    if (!driver) {
        close(gate[1]); char c; ssize_t n;
        do { n = read(gate[0], &c, 1); } while (n < 0 && errno == EINTR);
        close(gate[0]); if (n != 1) _exit(125);
        execv(argv[3], &argv[3]); _exit(127);
    }
    close(gate[0]);
    bool bound = bind_child(driver);
    if (!bound) failures++;
    if (bound) { if (write(gate[1], "r", 1) != 1) failures++; }
    close(gate[1]);
    long long deadline = now() + limit; int driver_code = -1; bool timed_out = false;
    for (;;) {
        if (now() >= deadline || now() < 0) timed_out = true;
        discover(proc); /* Before every exclusive reap, including adopted children. */
        if (timed_out || driver_code >= 0 || failures)
            for (size_t i = 0; i < count; i++) kill_bound(&children[i]);
        int status; pid_t pid = 0;
        while ((pid = waitpid(-1, &status, __WALL | WNOHANG)) > 0) {
            if (!WIFEXITED(status) && !WIFSIGNALED(status)) continue;
            reaped++;
            if (pid == driver) driver_code = WIFEXITED(status) ? WEXITSTATUS(status) : 128 + WTERMSIG(status);
            for (size_t i = 0; i < count; i++) if (children[i].pid == pid) {
                close(children[i].fd); children[i] = children[--count]; break;
            }
        }
        if (pid < 0 && errno == ECHILD) break;
        if (pid < 0 && errno != EINTR) failures++;
        poll(NULL, 0, 5);
    }
    fprintf(report, "{\"echild\":true,\"driverExitCode\":%d,\"emergencySignals\":%d,\"reaped\":%d,\"failures\":%d,\"deadline\":%s}\n",
        driver_code, emergency, reaped, failures, timed_out ? "true" : "false");
    fclose(report); closedir(proc);
    return failures ? 98 : 0;
}
