#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/pidfd.h>
#include <sys/prctl.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

/* Per-test emergency authority. Never installed, linked into the helper, or used
 * as its proof oracle. Positive tests require zero emergency lineage retirement. */
static long long millis(void) {
    struct timespec t; if (clock_gettime(CLOCK_MONOTONIC, &t)) return -1;
    return (long long)t.tv_sec * 1000 + t.tv_nsec / 1000000;
}
static void event(const char *dir, const char *kind) {
    char path[4096], line[160];
    if (snprintf(path, sizeof path, "%s/events.jsonl", dir) >= (int)sizeof path) _exit(90);
    int fd = open(path, O_WRONLY | O_APPEND | O_CREAT | O_CLOEXEC, 0600);
    int n = snprintf(line, sizeof line, "{\"kind\":\"%s\",\"pid\":%d,\"parent\":%d}\n", kind, getpid(), getppid());
    if (fd < 0 || write(fd, line, (size_t)n) != n) _exit(90);
    close(fd);
}
static void expiry(void) {
    struct sigaction sa = { .sa_handler = SIG_DFL }; sigemptyset(&sa.sa_mask);
    sigset_t mask; sigemptyset(&mask); sigaddset(&mask, SIGALRM);
    if (sigaction(SIGALRM, &sa, NULL) || sigprocmask(SIG_UNBLOCK, &mask, NULL)) _exit(90);
    alarm(7);
}
static void linger(const char *dir, const char *kind) {
    expiry(); signal(SIGTERM, SIG_IGN); event(dir, kind);
    for (;;) pause();
}
static volatile sig_atomic_t term_requested;
static void on_term(int sig) { (void)sig; term_requested = 1; }
static int worker(const char *dir, const char *mode) {
    expiry(); event(dir, "root");
    if (!strcmp(mode, "exit")) return 23;
    if (!strcmp(mode, "metadata")) {
        char c, cwd[4096]; const char *value = getenv("BOE_NATIVE_TEST_MARKER");
        if (read(STDIN_FILENO, &c, 1) == 0) event(dir, "stdin-eof");
        if (value && !strcmp(value, "synthetic-marker") && getcwd(cwd, sizeof cwd) && !strcmp(cwd, dir)) event(dir, "metadata-ok");
    }
    if (!strcmp(mode, "spawn") || !strcmp(mode, "spawn-window")) {
        struct sigaction sa = { .sa_handler = on_term }; sigemptyset(&sa.sa_mask);
        if (sigaction(SIGTERM, &sa, NULL)) return 91;
        event(dir, "prepared");
        while (!term_requested) {
            if (!strcmp(mode, "spawn-window")) {
                /* Deterministically deliver/pending-queue TERM after the flag
                 * check, before the wait. Fixture-only lost-wakeup regression. */
                event(dir, "before-wait");
                for (;;) {
                    sigset_t pending;
                    if (sigpending(&pending)) return 91;
                    if (term_requested || sigismember(&pending, SIGTERM)) break;
                    poll(NULL, 0, 1);
                }
            }
            pause();
        }
        pid_t p = fork(); if (p < 0) return 91;
        if (!p) linger(dir, "spawned");
        return 0;
    }
    if (!strcmp(mode, "ignore")) signal(SIGTERM, SIG_IGN);
    if (!strcmp(mode, "tree") || !strcmp(mode, "ignore") || !strcmp(mode, "root-first") || !strcmp(mode, "doublefork") || !strcmp(mode, "named")) {
        int ready[2]; if (pipe2(ready, O_CLOEXEC)) return 91;
        pid_t p = fork(); if (p < 0) return 91;
        if (!p) {
            expiry();
            close(ready[0]);
            if (!strcmp(mode, "named")) {
                if (prctl(PR_SET_NAME, "boe ) (\n leaf")) _exit(91);
                event(dir, "name-set");
            }
            if (!strcmp(mode, "doublefork")) {
                if (setsid() < 0) _exit(91);
                pid_t second = fork(); if (second < 0) _exit(91);
                if (second) { event(dir, "intermediate"); _exit(0); }
                expiry();
                if (setpgid(0, 0)) _exit(91);
                event(dir, "detached");
            }
            expiry(); signal(SIGTERM, SIG_IGN); event(dir, "leaf");
            if (write(ready[1], "r", 1) != 1) _exit(91);
            close(ready[1]); for (;;) pause();
        }
        close(ready[1]); char c;
        if (read(ready[0], &c, 1) != 1) return 91;
        close(ready[0]);
        if (!strcmp(mode, "root-first")) return 23;
    }
    event(dir, "prepared"); for (;;) pause();
}
static int stop_fd(int fd) { return pidfd_send_signal(fd, SIGKILL, NULL, 0) == 0 || errno == ESRCH ? 0 : -1; }
static void wait_owned(pid_t pid) {
    int s;
    for (;;) {
        pid_t p = waitpid(pid, &s, __WALL);
        if (p == pid && (WIFEXITED(s) || WIFSIGNALED(s))) return;
        if (p < 0 && errno == ECHILD) return;
        /* A deadline/error never disposes still-held exclusive reap authority. */
        if (p < 0 && errno != EINTR) poll(NULL, 0, 10);
    }
}
static pid_t fork_bound(int *fd) {
    int gate[2]; if (pipe2(gate, O_CLOEXEC)) return -1;
    pid_t p = fork();
    if (p < 0) { close(gate[0]); close(gate[1]); return -1; }
    if (!p) {
        close(gate[1]); char c; ssize_t n;
        do { n = read(gate[0], &c, 1); } while (n < 0 && errno == EINTR);
        close(gate[0]); if (n != 1 || c != 'r') _exit(125);
        return 0;
    }
    close(gate[0]); *fd = pidfd_open(p, 0);
    if (*fd < 0) { close(gate[1]); wait_owned(p); return -1; }
    ssize_t n; do { n = write(gate[1], "r", 1); } while (n < 0 && errno == EINTR);
    (void)n; close(gate[1]); /* Even failed release retains p/fd for the caller. */
    return p;
}
static int stale_check(void) {
    int stale = -1, live = -1;
    pid_t p = fork_bound(&stale); if (p < 0) return 91; if (!p) _exit(0);
    wait_owned(p);
    pid_t sentinel = fork_bound(&live); if (sentinel < 0) { close(stale); return 91; }
    if (!sentinel) { alarm(7); for (;;) pause(); }
    errno = 0; int result = pidfd_send_signal(stale, SIGKILL, NULL, 0), error = errno;
    int alive = pidfd_send_signal(live, 0, NULL, 0) == 0;
    stop_fd(live); wait_owned(sentinel); close(stale); close(live);
    int s; errno = 0; int empty = waitpid(-1, &s, __WALL | WNOHANG) == -1 && errno == ECHILD;
    printf("{\"staleResult\":%d,\"errno\":%d,\"sentinelAlive\":%s,\"echild\":%s}\n", result, error, alive ? "true" : "false", empty ? "true" : "false");
    return result == -1 && error == ESRCH && alive && empty ? 0 : 95;
}
static int guardian(const char *dir, const char *mode, char **helper_argv) {
    signal(SIGPIPE, SIG_IGN); signal(SIGCHLD, SIG_DFL);
    sigset_t empty; sigemptyset(&empty); if (sigprocmask(SIG_SETMASK, &empty, NULL) || prctl(PR_SET_CHILD_SUBREAPER, 1)) return 96;
    /* Independent guardian: no proc enumeration, only held direct-fork pidfds
     * and exclusive wait/reap of bounded, independently expiring fixture actors. */
    char children[4096], report[4096];
    if (snprintf(report, sizeof report, "%s/guardian.json", dir) >= (int)sizeof report) return 98;
    if (!strcmp(mode, "missing-proc-fixture")) {
        if (snprintf(children, sizeof children, "%s/absent-proc-children", dir) >= (int)sizeof children) return 98;
    } else children[0] = 0;
    FILE *result_file = fopen(report, "we"); if (!result_file) return 98;
    long long start_time = millis();
    int capability_error = start_time < 0 ? errno : 0, self = pidfd_open(getpid(), 0);
    if (self < 0) capability_error = errno;
    else { if (pidfd_send_signal(self, 0, NULL, 0)) capability_error = errno; close(self); }
    if (children[0]) { /* Explicit fixture-only admission failure, no real proc dependency. */
        FILE *preflight = fopen(children, "re");
        if (!preflight) capability_error = errno;
        else fclose(preflight);
    }
    if (capability_error) {
        int s; errno = 0; int no_children = waitpid(-1, &s, __WALL | WNOHANG) == -1 && errno == ECHILD;
        fprintf(result_file, "{\"unavailable\":true,\"errno\":%d,\"helperCreated\":false,\"sentinelCreated\":false,\"echild\":%s}\n", capability_error, no_children ? "true" : "false");
        fclose(result_file); return 78;
    }
    int blocked[2] = {-1, -1};
    if (!strcmp(mode, "blocked-status")) {
        if (pipe2(blocked, O_CLOEXEC | O_NONBLOCK)) { fclose(result_file); return 96; }
        char fill[4096]; memset(fill, 'x', sizeof fill);
        while (write(blocked[1], fill, sizeof fill) > 0) { }
        if (errno != EAGAIN) { close(blocked[0]); close(blocked[1]); fclose(result_file); return 96; }
    }
    pid_t sentinel = -1; int sentinel_fd = -1;
    if (!strcmp(mode, "sentinel")) {
        sentinel = fork_bound(&sentinel_fd); if (sentinel < 0) { fclose(result_file); return 91; }
        if (!sentinel) { close(0); close(1); close(2); linger(dir, "outside-sentinel"); }
    }
    int helper_fd = -1;
    pid_t helper = fork_bound(&helper_fd);
    if (helper < 0) {
        if (sentinel_fd >= 0) { stop_fd(sentinel_fd); wait_owned(sentinel); close(sentinel_fd); }
        fclose(result_file); return 91;
    }
    if (!helper) {
        if (blocked[1] >= 0 && dup2(blocked[1], STDOUT_FILENO) < 0) _exit(96);
        execv(helper_argv[0], helper_argv); _exit(127);
    }
    close(STDIN_FILENO); close(STDOUT_FILENO);
    if (blocked[1] >= 0) close(blocked[1]);
    int helper_code = -1, emergency = 0, alarm_reaps = 0, sentinel_alive = 0, sentinel_reaped = 0, deadline = 0, failure = 0, sentinel_stopped = 0, crash_requested = 0;
    char crash_request[4096];
    /* dir/report path length already admitted; the request suffix is shorter. */
    if (snprintf(crash_request, sizeof crash_request, "%s/crash", dir) >= (int)sizeof crash_request) { failure = 1; stop_fd(helper_fd); }
    long long end = start_time + 20000;
    for (;;) {
        long long current_time = millis();
        if (current_time < 0) { failure = 1; stop_fd(helper_fd); if (sentinel_fd >= 0) stop_fd(sentinel_fd); }
        if ((current_time < 0 || current_time >= end) && !deadline) { deadline = 1; if (stop_fd(helper_fd)) failure = 1; }
        if (!crash_requested && !strcmp(mode, "crash-helper") && access(crash_request, F_OK) == 0) {
            crash_requested = 1; if (stop_fd(helper_fd)) failure = 1;
        }
        if (helper_code >= 0 && sentinel_fd >= 0 && !sentinel_stopped) {
            struct pollfd observed = { .fd = sentinel_fd, .events = POLLIN };
            sentinel_alive = poll(&observed, 1, 0) == 0;
            if (stop_fd(sentinel_fd)) failure = 1;
            sentinel_stopped = 1;
        }
        int status; pid_t p;
        while ((p = waitpid(-1, &status, __WALL | WNOHANG)) > 0) {
            if (!WIFEXITED(status) && !WIFSIGNALED(status)) continue;
            if (p == helper) helper_code = WIFEXITED(status) ? WEXITSTATUS(status) : 128 + WTERMSIG(status);
            else if (p == sentinel) sentinel_reaped = 1;
            else { emergency++; if (WIFSIGNALED(status) && WTERMSIG(status) == SIGALRM) alarm_reaps++; }
        }
        if (p < 0 && errno == ECHILD) break;
        if (p < 0 && errno != EINTR) { failure = 1; stop_fd(helper_fd); if (sentinel_fd >= 0) stop_fd(sentinel_fd); }
        poll(NULL, 0, 10);
    }
    if (blocked[0] >= 0) close(blocked[0]);
    close(helper_fd); if (sentinel_fd >= 0) close(sentinel_fd);
    fprintf(result_file, "{\"helperExitCode\":%d,\"emergencyLineageActions\":%d,\"emergencyAlarmReaps\":%d,\"deadline\":%s,\"failure\":%s,\"sentinelAliveAfterHelper\":%s,\"sentinelReaped\":%s,\"echild\":true}\n", helper_code, emergency, alarm_reaps, deadline ? "true" : "false", failure ? "true" : "false", sentinel_alive ? "true" : "false", sentinel_reaped ? "true" : "false");
    if (fclose(result_file)) return 98;
    return emergency || deadline || failure ? 99 : 0;
}
int main(int argc, char **argv) {
    if (argc == 2 && !strcmp(argv[1], "--pidfd-check")) return stale_check();
    if (argc == 4 && !strcmp(argv[1], "--worker")) return worker(argv[2], argv[3]);
    if (argc >= 5 && !strcmp(argv[1], "--guard")) return guardian(argv[2], argv[3], &argv[4]);
    return 100;
}
