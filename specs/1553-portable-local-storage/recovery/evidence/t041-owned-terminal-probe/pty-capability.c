#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/pidfd.h>
#include <sys/wait.h>
#include <termios.h>
#include <time.h>
#include <unistd.h>

/* Throwaway capability probe, NOT a runtime adapter. One fixed direct child,
 * no grandchildren. Execute only under the existing independent host guardian. */
static volatile sig_atomic_t resized;
static void winch(int unused) { (void)unused; resized = 1; }
static long long now_ms(void) {
    struct timespec t;
    if (clock_gettime(CLOCK_MONOTONIC, &t)) return -1;
    return (long long)t.tv_sec * 1000 + t.tv_nsec / 1000000;
}
static bool put(int fd, const char *s) {
    size_t left = strlen(s);
    while (left) {
        ssize_t n = write(fd, s, left);
        if (n < 0 && errno == EINTR) continue;
        if (n <= 0) return false;
        s += n; left -= (size_t)n;
    }
    return true;
}
static bool until(int fd, char *buffer, size_t *used, const char *marker) {
    long long start = now_ms(), end = start + 1200;
    if (start < 0) return false;
    while (now_ms() >= 0 && now_ms() < end) {
        if (strstr(buffer, marker)) return true;
        struct pollfd p = { .fd = fd, .events = POLLIN };
        int ready = poll(&p, 1, 20);
        if (ready < 0 && errno == EINTR) continue;
        if (ready < 0 || *used >= 4095) return false;
        if (!ready) continue;
        ssize_t n = read(fd, buffer + *used, 4095 - *used);
        if (n < 0 && (errno == EINTR || errno == EAGAIN)) continue;
        if (n <= 0) return false;
        *used += (size_t)n; buffer[*used] = 0;
    }
    return false;
}
static void fixture(int master, int slave, int gate) {
    alarm(7); close(master);
    char c; ssize_t n;
    do { n = read(gate, &c, 1); } while (n < 0 && errno == EINTR);
    close(gate); if (n != 1) _exit(125);
    if (setsid() < 0 || ioctl(slave, TIOCSCTTY, 0) || tcsetpgrp(slave, getpid())) _exit(71);
    if (dup2(slave, 0) < 0 || dup2(slave, 1) < 0 || dup2(slave, 2) < 0) _exit(72);
    if (slave > 2) close(slave);
    struct sigaction sa = { .sa_handler = winch }; sigemptyset(&sa.sa_mask);
    if (sigaction(SIGWINCH, &sa, NULL)) _exit(73);
    struct winsize size;
    if (ioctl(0, TIOCGWINSZ, &size)) _exit(74);
    int owned = isatty(0) && isatty(1) && isatty(2) && getsid(0) == getpid() &&
        getpgrp() == getpid() && tcgetpgrp(0) == getpid() && tcgetsid(0) == getpid();
    dprintf(1, "READY owned=%d size=%ux%u\n", owned, size.ws_row, size.ws_col);
    int lines = 0;
    for (;;) {
        if (resized) {
            resized = 0;
            if (ioctl(0, TIOCGWINSZ, &size)) _exit(75);
            dprintf(1, "RESIZE %ux%u\n", size.ws_row, size.ws_col);
        }
        struct pollfd p = { .fd = 0, .events = POLLIN };
        int ready = poll(&p, 1, 20);
        if (ready < 0 && errno == EINTR) continue;
        if (ready < 0) _exit(76);
        if (!ready) continue;
        char line[128]; n = read(0, line, sizeof line - 1);
        if (n < 0 && errno == EINTR) continue;
        if (n < 0) _exit(77);
        if (!n) { put(1, "INPUT_EOF_STILL_ALIVE\n"); for (;;) pause(); }
        line[n] = 0; dprintf(1, "LINE%d:%s", ++lines, line);
    }
}
int main(void) {
    int master = -1, slave = -1, pidfd = -1, gate[2] = {-1, -1}, saved = 0;
    pid_t child = -1; int status = -1, hangup_errno = 0;
    bool allocated = false, control = false, unicode = false, resize = false,
        eof_alive = false, exact_reap = false, echild = false, hangup = false;
    char buffer[4096] = {0}; size_t used = 0;
    master = posix_openpt(O_RDWR | O_NOCTTY | O_CLOEXEC);
    if (master < 0 || grantpt(master) || unlockpt(master)) goto cleanup;
    char name[128];
    if (ptsname_r(master, name, sizeof name)) goto cleanup;
    slave = open(name, O_RDWR | O_NOCTTY | O_CLOEXEC);
    if (slave < 0) goto cleanup;
    struct termios t;
    if (tcgetattr(slave, &t)) goto cleanup;
    t.c_lflag |= ICANON; t.c_cc[VEOF] = 4;
    if (tcsetattr(slave, TCSANOW, &t)) goto cleanup;
    struct winsize size = { .ws_row = 25, .ws_col = 80 };
    if (ioctl(master, TIOCSWINSZ, &size) || pipe2(gate, O_CLOEXEC)) goto cleanup;
    allocated = true;
    child = fork(); if (child < 0) goto cleanup;
    if (!child) { close(gate[1]); fixture(master, slave, gate[0]); _exit(78); }
    close(gate[0]); gate[0] = -1; close(slave); slave = -1;
    pidfd = pidfd_open(child, 0); /* Exact unreaped direct child; sole reaper. */
    if (pidfd < 0 || !put(gate[1], "L")) goto cleanup;
    close(gate[1]); gate[1] = -1;
    if (!(control = until(master, buffer, &used, "READY owned=1 size=25x80"))) goto cleanup;
    if (!put(master, "alpha π\n") || !until(master, buffer, &used, "LINE1:alpha π")) goto cleanup;
    if (!put(master, "beta\n") || !until(master, buffer, &used, "LINE2:beta")) goto cleanup;
    unicode = true;
    size.ws_row = 31; size.ws_col = 93;
    if (ioctl(master, TIOCSWINSZ, &size) || !(resize = until(master, buffer, &used, "RESIZE 31x93"))) goto cleanup;
    if (!put(master, "\004") || !until(master, buffer, &used, "INPUT_EOF_STILL_ALIVE")) goto cleanup;
    struct pollfd p = { .fd = pidfd, .events = POLLIN };
    eof_alive = poll(&p, 1, 0) == 0;
cleanup:
    saved = errno;
    for (int i = 0; i < 2; i++) if (gate[i] >= 0) close(gate[i]);
    if (slave >= 0) close(slave);
    if (child > 0) {
        /* Never signal a guessed PID/group. If pidfd acquisition failed, closing
         * the still-held gate lets the fixture exit before terminal attachment. */
        if (pidfd >= 0 && pidfd_send_signal(pidfd, SIGTERM, NULL, 0) && errno != ESRCH) saved = errno;
        pid_t result; do { result = waitpid(child, &status, 0); } while (result < 0 && errno == EINTR);
        exact_reap = result == child && WIFSIGNALED(status) && WTERMSIG(status) == SIGTERM;
        echild = waitpid(-1, &status, WNOHANG) < 0 && errno == ECHILD;
        if (pidfd >= 0) close(pidfd);
    }
    if (master >= 0) {
        long long end = now_ms() + 1200;
        while (child > 0 && now_ms() >= 0 && now_ms() < end) {
            struct pollfd p = { .fd = master, .events = POLLIN };
            if (poll(&p, 1, 20) <= 0) continue;
            char tail[128]; ssize_t n = read(master, tail, sizeof tail);
            if (!n) { hangup = true; break; }
            if (n < 0 && errno != EINTR && errno != EAGAIN) { hangup_errno = errno; hangup = errno == EIO; break; }
        }
        close(master);
    }
    if (used) fwrite(buffer, 1, used, stderr);
    printf("{\"allocated\":%s,\"controlTerminal\":%s,\"twoInputsUnicode\":%s,\"resizeSignal\":%s,\"canonicalEofChildAlive\":%s,\"exactPidfdTermReap\":%s,\"echild\":%s,\"masterHangup\":%s,\"masterHangupErrno\":%d,\"lastErrnoDiagnostic\":%d}\n",
        allocated ? "true":"false", control ? "true":"false", unicode ? "true":"false", resize ? "true":"false",
        eof_alive ? "true":"false", exact_reap ? "true":"false", echild ? "true":"false", hangup ? "true":"false", hangup_errno, saved);
    return allocated && control && unicode && resize && eof_alive && exact_reap && echild && hangup ? 0 : 1;
}
