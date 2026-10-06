#define _GNU_SOURCE
#include <errno.h>
#include <poll.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <string.h>
#include <sys/ioctl.h>
#include <termios.h>
#include <unistd.h>

/* Fixed inert CLI: input is text, never a shell command/GM/save operation. */
static volatile sig_atomic_t resized;
static void winch(int unused) { (void)unused; resized = 1; }
static void view(const char *draft) { dprintf(1, "\033[2J\033[HNEUTRAL READY\r\n> %s <\r\n", draft); }
int main(void) {
    alarm(8);
    if (!isatty(0) || !isatty(1) || !isatty(2) || tcgetsid(0) != getpid() || tcgetpgrp(0) != getpid()) return 78;
    struct termios original, raw;
    if (tcgetattr(0, &original)) return 79;
    raw = original; cfmakeraw(&raw);
    if (tcsetattr(0, TCSANOW, &raw)) return 80;
    struct sigaction sa = { .sa_handler = winch }; sigemptyset(&sa.sa_mask);
    if (sigaction(SIGWINCH, &sa, NULL)) return 81;
    dprintf(1, "TTY_READY owned=1 pid=%d sid=%d\r\n", getpid(), getsid(0)); view("");
    char draft[4096] = {0}; size_t used = 0; int submitted = 0; bool canonical = false;
    for (;;) {
        if (resized) {
            resized = 0; struct winsize size;
            if (ioctl(0, TIOCGWINSZ, &size)) return 82;
            dprintf(1, "RESIZE %ux%u\r\n", size.ws_row, size.ws_col);
        }
        struct pollfd p = { .fd = 0, .events = POLLIN };
        int ready = poll(&p, 1, 20);
        if (ready < 0 && errno == EINTR) continue;
        if (ready < 0) return 83;
        if (!ready) continue;
        char bytes[128]; ssize_t count = read(0, bytes, sizeof bytes);
        if (count < 0 && errno == EINTR) continue;
        if (count < 0) return 84;
        if (!count) {
            if (!canonical) return 85;
            dprintf(1, "CANONICAL_EOF\r\n"); canonical = false;
            if (tcsetattr(0, TCSANOW, &raw)) return 86;
            view(""); continue;
        }
        for (ssize_t i = 0; i < count; i++) {
            if (bytes[i] == '\r' || bytes[i] == '\r\n') {
                draft[used] = 0;
                if (!strcmp(draft, "canonical")) {
                    struct termios cooked = raw; cooked.c_lflag |= ICANON; cooked.c_cc[VEOF] = 4;
                    if (tcsetattr(0, TCSANOW, &cooked)) return 87;
                    canonical = true; dprintf(1, "CANONICAL_READY\r\n");
                } else { dprintf(1, "NEUTRAL WORKING\r\nRESULT%d:%s\r\n", ++submitted, draft); usleep(50000); view(""); }
                used = 0; draft[0] = 0;
            } else if (used < sizeof draft - 1) { draft[used++] = bytes[i]; draft[used] = 0; }
            else return 88;
        }
        if (!canonical && used) view(draft);
    }
}
