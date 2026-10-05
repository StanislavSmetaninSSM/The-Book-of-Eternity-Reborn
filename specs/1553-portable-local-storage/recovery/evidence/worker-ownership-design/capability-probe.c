#define _GNU_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/prctl.h>
#include <sys/pidfd.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>

/* Capability evidence only. One synthetic child, never an arbitrary command. */
static void row(const char *call, int rc, int error) {
    printf("{\"call\":\"%s\",\"rc\":%d,\"errno\":%d}\n", call, rc, error);
    fflush(stdout);
}
int main(void) {
    struct sigaction disposition = { .sa_handler = SIG_DFL };
    sigemptyset(&disposition.sa_mask);
    if (sigaction(SIGCHLD, &disposition, NULL) != 0) return 10;
    errno = 0;
    int rc = prctl(PR_SET_CHILD_SUBREAPER, 1L, 0L, 0L, 0L);
    row("prctl(PR_SET_CHILD_SUBREAPER,1)", rc, errno);
    if (rc != 0) return 20;
    int enabled = 0;
    errno = 0;
    rc = prctl(PR_GET_CHILD_SUBREAPER, &enabled, 0L, 0L, 0L);
    row("prctl(PR_GET_CHILD_SUBREAPER)", rc, errno);
    if (rc != 0 || enabled != 1) return 21;
    int ready[2];
    if (pipe2(ready, O_CLOEXEC) != 0) return 22;
    pid_t child = fork();
    if (child < 0) { row("fork", -1, errno); return 23; }
    if (child == 0) {
        close(ready[0]);
        (void)sigaction(SIGALRM, &disposition, NULL);
        alarm(3); /* Independent bounded exit even if parent/pidfd operation fails. */
        if (write(ready[1], "R", 1) != 1) _exit(24);
        close(ready[1]);
        for (;;) pause();
    }
    close(ready[1]);
    row("owned-child-pid", child, 0);
    char value = 0;
    ssize_t got;
    do { got = read(ready[0], &value, 1); } while (got < 0 && errno == EINTR);
    close(ready[0]);
    int failed = got != 1 || value != 'R';
    errno = 0;
    int fd = pidfd_open(child, 0);
    row("pidfd_open(owned-unreaped-child,0)", fd < 0 ? -1 : 0, errno);
    if (fd < 0) failed = 1;
    if (fd >= 0 && !failed) {
        errno = 0;
        rc = pidfd_send_signal(fd, 0, NULL, 0);
        row("pidfd_send_signal(signal0)", rc, errno);
        if (rc != 0) failed = 1;
    }
    if (fd >= 0 && !failed) {
        errno = 0;
        rc = pidfd_send_signal(fd, SIGKILL, NULL, 0);
        row("pidfd_send_signal(SIGKILL,owned-child)", rc, errno);
        if (rc != 0) failed = 1;
    }
    /* No fallback signaling on denial: the child self-expires after 3 seconds. */
    int status = 0;
    do { errno = 0; rc = waitpid(child, &status, __WALL); } while (rc < 0 && errno == EINTR);
    row("waitpid(owned-child,__WALL)", rc == child ? 0 : rc, errno);
    if (rc != child || !WIFSIGNALED(status)) failed = 1;
    row("owned-child-terminal-signal", WIFSIGNALED(status) ? WTERMSIG(status) : -1, 0);
    if (fd >= 0) {
        errno = 0;
        rc = pidfd_send_signal(fd, 0, NULL, 0);
        int saved = errno;
        row("pidfd_send_signal(stale-fd,signal0)", rc, saved);
        if (rc != -1 || saved != ESRCH) failed = 1;
        close(fd);
    }
    errno = 0;
    rc = waitpid(-1, &status, __WALL | WNOHANG);
    int saved = errno;
    row("waitpid(all,__WALL|WNOHANG)", rc, saved);
    if (rc != -1 || saved != ECHILD) failed = 1;
    return failed ? 25 : 0;
}
