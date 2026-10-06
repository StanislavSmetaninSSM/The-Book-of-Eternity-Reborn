import os,sys,pty,fcntl,termios,struct,select,time,signal,pathlib,json,errno
out=pathlib.Path(sys.argv[1]); scratch=out/'empty-scratch'; scratch.mkdir(exist_ok=False)
assert not list(scratch.iterdir())
master,slave=pty.openpty(); fcntl.ioctl(slave,termios.TIOCSWINSZ,struct.pack('HHHH',25,100,0,0))
pid=os.fork()
if pid==0:
    os.close(master); os.setsid(); fcntl.ioctl(slave,termios.TIOCSCTTY,0)
    for fd in (0,1,2): os.dup2(slave,fd)
    if slave>2: os.close(slave)
    os.chdir(scratch); os.execv('/opt/codex/bin/codex',['codex','--no-daemon','-C',str(scratch)])
os.close(slave); pidfd=os.pidfd_open(pid); started=time.monotonic(); captured=bytearray(); inputs=[]; status=None; eof=False
sent=0; signalled=[]; scan=0
while time.monotonic()-started<8:
    elapsed=time.monotonic()-started
    if status is None and elapsed>=4+sent*.4 and sent<2:
        os.write(master,b'\x03'); inputs.append({'atSeconds':round(elapsed,3),'hex':'03','purpose':'exit-only Ctrl+C; no acceptance or prompt'}); sent+=1
    if status is None and elapsed>=6 and not signalled:
        signal.pidfd_send_signal(pidfd,signal.SIGTERM); signalled.append('SIGTERM original unreaped root pidfd')
    if status is None and elapsed>=7 and len(signalled)==1:
        signal.pidfd_send_signal(pidfd,signal.SIGKILL); signalled.append('SIGKILL original unreaped root pidfd')
    if not eof and select.select([master],[],[],.05)[0]:
        try: data=os.read(master,8192)
        except OSError as e:
            if e.errno!=errno.EIO: raise
            data=b''
        if not data: eof=True
        if data:
            captured.extend(data)
            if len(captured)>262144: raise RuntimeError('bounded capture exceeded')
            # Standard terminal cursor report only, not a trust/access response.
            while True:
                pos=captured.find(b'\x1b[6n',scan)
                if pos<0: scan=max(scan,len(captured)-3); break
                os.write(master,b'\x1b[1;1R'); inputs.append({'atSeconds':round(elapsed,3),'hex':'1b5b313b3152','purpose':'PTY cursor-position terminal query'}); scan=pos+4
    if status is None:
        reaped,s=os.waitpid(pid,os.WNOHANG)
        if reaped: status=s
    if status is not None and eof: break
os.close(master); os.close(pidfd)
if status is None: raise RuntimeError('original root was not reaped within bound')
(out/'startup.raw').write_bytes(captured)
(out/'startup-readable.txt').write_text(captured.decode('utf-8',errors='replace').replace('\x1b','<ESC>'))
(out/'probe-result.json').write_text(json.dumps({'argv':['/opt/codex/bin/codex','--no-daemon','-C',str(scratch)],'noPromptArgument':True,'noModelInput':True,'ptyRetainedUntilRootReap':True,'dimensions':[100,25],'rootPid':pid,'exitCode':os.waitstatus_to_exitcode(status),'elapsedSeconds':round(time.monotonic()-started,3),'inputJournal':inputs,'scopedSignals':signalled,'rawBytes':len(captured),'scratchFilesAfter':sorted(str(p.relative_to(scratch)) for p in scratch.rglob('*'))},indent=2)+'\n')
print(json.dumps({'exitCode':os.waitstatus_to_exitcode(status),'rawBytes':len(captured),'inputs':len(inputs),'scratchFilesAfter':len(list(scratch.rglob('*')))}))
