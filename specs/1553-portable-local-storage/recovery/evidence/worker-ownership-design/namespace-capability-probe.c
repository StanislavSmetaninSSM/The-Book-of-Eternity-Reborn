#define _GNU_SOURCE
#include <errno.h>
#include <sched.h>
#include <stdio.h>
#include <string.h>
/* One normal-permission namespace creation attempt. No fork, exec, UID map,
   mount, sysctl, cgroup, alternate flags or privileged retry follows it. */
int main(void) {
    errno = 0;
    int rc = unshare(CLONE_NEWUSER | CLONE_NEWPID);
    int error = errno;
    printf("{\"call\":\"unshare(CLONE_NEWUSER|CLONE_NEWPID)\",\"rc\":%d,\"errno\":%d,\"error\":\"%s\",\"childrenCreated\":0}\n", rc, error, strerror(error));
    return rc == 0 ? 0 : 20;
}
