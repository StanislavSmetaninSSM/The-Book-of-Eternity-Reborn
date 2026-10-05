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
    struct timespec t; if (clock_gettime(CLOCK_MONOTONIC, &t)) abort();
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
static void linger(const char *dir, const char *kind) {
    alarm(7); signal(SIGTERM, SIG_IGN); event(dir, kind);
    for (;;) pause();
}
static volatile sig_atomic_t term_requested;
static void on_term(int sig) { (void)sig; term_requested = 1; }
static int worker(const char *dir, const char *mode) {
    alarm(7); event(dir, "root");
    if (!strcmp(mode, "exit")) return 23;
    if (!strcmp(mode, "metadata")) {
        char c, cwd[4096]; const char *value = getenv("BOE_NATIVE_TEST_MARKER");
        if (read(STDIN_FILENO, &c, 1) == 0) event(dir, "stdin-eof");
        if (value && !strcmp(value, "synthetic-marker") && getcwd(cwd, sizeof cwd) && !strcmp(cwd, dir)) event(dir, "metadata-ok");
    }
    if (!strcmp(mode, "spawn")) {
        struct sigaction sa = { .sa_handler = on_term }; sigemptyset(&sa.sa_mask);
        if (sigaction(SIGTERM, &sa, NULL)) return 91;
        event(dir, "prepared");
        while (!term_requested) pause();
        pid_t p = fork(); if (p < 0) return 91;
        if (!p) linger(dir, "spawned");
        return 0;
    }
    if (!strcmp(mode, "ignore")) signal(SIGTERM, SIG_IGN);
    if (!strcmp(mode, "tree") || !strcmp(mode, "ignore") || !strcmp(mode, "root-first") || !strcmp(mode, "doublefork")) {
        int ready[2]; if (pipe2(ready, O_CLOEXEC)) return 91;
        pid_t p = fork(); if (p < 0) return 91;
        if (!p) {
            close(ready[0]);
            if (!strcmp(mode, "doublefork")) {
                if (setsid() < 0) _exit(91);
                pid_t second = fork(); if (second < 0) _exit(91);
                if (second) { event(dir, "intermediate"); _exit(0); }
                if (setpgid(0, 0)) _exit(91);
                event(dir, "detached");
            }
            alarm(7); signal(SIGTERM, SIG_IGN); event(dir, "leaf");
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
static int bind_child(pid_t pid) { int fd = pidfd_open(pid, 0); if (fd < 0) _exit(92); return fd; }
static void stop_fd(int fd) { if (pidfd_send_signal(fd, SIGKILL, NULL, 0) && errno != ESRCH) _exit(93); }
static void wait_owned(pid_t pid) { int s; while (waitpid(pid, &s, __WALL) < 0) { if (errno != EINTR) _exit(94); } }
static int stale_check(void) {
    pid_t p = fork(); if (p < 0) return 91; if (!p) _exit(0);
    int stale = bind_child(p); wait_owned(p);
    pid_t sentinel = fork(); if (sentinel < 0) return 91;
    if (!sentinel) { alarm(7); for (;;) pause(); }
    int live = bind_child(sentinel);
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
    int blocked[2] = {-1, -1};
    if (!strcmp(mode, "blocked-status")) {
        if (pipe2(blocked, O_CLOEXEC | O_NONBLOCK)) return 96;
        char fill[4096]; memset(fill, 'x', sizeof fill);
        while (write(blocked[1], fill, sizeof fill) > 0) { }
        if (errno != EAGAIN) return 96;
    }
    pid_t sentinel = -1; int sentinel_fd = -1;
    if (!strcmp(mode, "sentinel")) {
        sentinel = fork(); if (sentinel < 0) return 91;
        if (!sentinel) { close(0); close(1); close(2); linger(dir, "outside-sentinel"); }
        sentinel_fd = bind_child(sentinel);
    }
    pid_t helper = fork(); if (helper < 0) return 91;
    if (!helper) {
        if (blocked[1] >= 0 && dup2(blocked[1], STDOUT_FILENO) < 0) _exit(96);
        execv(helper_argv[0], helper_argv); _exit(127);
    }
    int helper_fd = bind_child(helper); close(STDIN_FILENO); close(STDOUT_FILENO);
    if (blocked[1] >= 0) close(blocked[1]);
    int helper_code = -1, emergency = 0, sentinel_alive = 0, sentinel_reaped = 0, deadline = 0;
    long long end = millis() + 20000;
    for (;;) {
        if (millis() >= end && !deadline) { deadline = 1; stop_fd(helper_fd); }
        /* Snapshot only to acquire handles before this sole reaper reaps. */
        char path[128]; snprintf(path, sizeof path, "/proc/self/task/%d/children", getpid());
        FILE *f = fopen(path, "re"); if (!f) return 97;
        int pid;
        while (fscanf(f, "%d", &pid) == 1) {
            if (pid == sentinel) {
                if (helper_code >= 0) {
                    sentinel_alive = pidfd_send_signal(sentinel_fd, 0, NULL, 0) == 0;
                    stop_fd(sentinel_fd);
                }
            } else if (pid != helper && (helper_code >= 0 || deadline)) {
                int fd = bind_child(pid); stop_fd(fd); close(fd); emergency++;
            }
        }
        fclose(f);
        int status; pid_t p;
        while ((p = waitpid(-1, &status, __WALL | WNOHANG)) > 0) {
            if (!WIFEXITED(status) && !WIFSIGNALED(status)) continue;
            if (p == helper) helper_code = WIFEXITED(status) ? WEXITSTATUS(status) : 128 + WTERMSIG(status);
            else if (p == sentinel) sentinel_reaped = 1;
            else emergency++;
        }
        if (p < 0 && errno == ECHILD) break;
        if (p < 0 && errno != EINTR) return 97;
        poll(NULL, 0, 10);
    }
    if (blocked[0] >= 0) close(blocked[0]);
    close(helper_fd); if (sentinel_fd >= 0) close(sentinel_fd);
    char path[4096]; if (snprintf(path, sizeof path, "%s/guardian.json", dir) >= (int)sizeof path) return 98;
    FILE *f = fopen(path, "we"); if (!f) return 98;
    fprintf(f, "{\"helperExitCode\":%d,\"emergencyLineageActions\":%d,\"deadline\":%s,\"sentinelAliveAfterHelper\":%s,\"sentinelReaped\":%s,\"echild\":true}\n", helper_code, emergency, deadline ? "true" : "false", sentinel_alive ? "true" : "false", sentinel_reaped ? "true" : "false");
    if (fclose(f)) return 98;
    return emergency || deadline ? 99 : 0;
}
int main(int argc, char **argv) {
    if (argc == 2 && !strcmp(argv[1], "--pidfd-check")) return stale_check();
    if (argc == 4 && !strcmp(argv[1], "--worker")) return worker(argv[2], argv[3]);
    if (argc >= 5 && !strcmp(argv[1], "--guard")) return guardian(argv[2], argv[3], &argv[4]);
    return 100;
}
