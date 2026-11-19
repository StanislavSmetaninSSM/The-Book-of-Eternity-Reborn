"""Inert transport DATA controls. No Node/.NET child or package import."""
import io, json, os, pathlib, tempfile, time, types, unittest
from unittest.mock import patch
from owned_terminal import OwnedTerminal
from pty_probe import PtyWriter

class Transport(OwnedTerminal):
    def __init__(self, folder, reply):
        self.deadline=time.monotonic()+.2;self.writer=lambda *args:None
        self.work_deadline=self.deadline;self.cleanup_deadline=self.deadline+.2;self.cleanup_entered=False
        self.seq=0;self.failed=False;self.closed=False;self.pending=False;self.buffer=b''
        self.audit=[];self.evidence=pathlib.Path(folder);self.reply=reply
        self.readfd,writefd=os.pipe();os.set_blocking(writefd,False)
        self.process=types.SimpleNamespace(stdin=os.fdopen(writefd,'wb',buffering=0))
    def _read(self):
        self._time()
        return self.reply
    def release(self):
        self.process.stdin.close();os.close(self.readfd)

class Controls(unittest.TestCase):
    def setUp(self):
        self.folder=tempfile.TemporaryDirectory();self.addCleanup(self.folder.cleanup)
    def transport(self,reply):
        value=Transport(self.folder.name,reply);self.addCleanup(value.release);return value
    def test_callback_reply_precedes_key_and_pending_blocks_key(self):
        t=self.transport({'op':'WRITTEN','seq':1,'pending':False,'replies':[{'bytes':'G1sxOzRS','state':{'x':3,'y':0}}],'cursor':{'x':3,'y':0},'modes':{**dict.fromkeys(['applicationCursorKeysMode','applicationKeypadMode','bracketedPasteMode','insertMode','originMode','reverseWraparoundMode','sendFocusMode','synchronizedOutputMode','wraparoundMode'],False),'mouseTrackingMode':'none'}})
        sent=[];t.writer=lambda *value:sent.append(value)
        t.feed(b'abc\x1b[6n');t.key(b'x','one')
        self.assertEqual([x[1] for x in sent],['terminal-reply','route-key:one'])
        t.pending=True
        with self.assertRaises(ValueError):t.key(b'x','two')
        self.assertEqual(len(sent),2)
    def test_one_way_cleanup_uses_original_bounds_and_blocks_work_keys(self):
        t=self.transport({});sent=[];t.writer=lambda *args:sent.append(args)
        original=t.cleanup_deadline;t.deadline=t.work_deadline=0
        t.enter_cleanup()
        self.assertEqual(t.deadline,original)
        t.key(b'x','observed-cleanup',cleanup=True)
        self.assertEqual(len(sent),1)
        with self.assertRaises(ValueError):t.enter_cleanup()
        with self.assertRaises(ValueError):t.key(b'x','work-forbidden')
        self.assertEqual(len(sent),1)
    def test_expired_cleanup_never_extends_or_writes(self):
        t=self.transport({});t.deadline=t.work_deadline=t.cleanup_deadline=0
        with self.assertRaises(TimeoutError):t.enter_cleanup()
        self.assertTrue(t.failed)
        with self.assertRaises(ValueError):t.key(b'x','late',cleanup=True)
        self.assertEqual(t.deadline,0)
    def test_client_wrapper_preserves_explicit_cleanup_intent(self):
        from client_session import ClientSession
        for ordinary in (True,False):
            t=self.transport({});sent=[];t.writer=lambda *args:sent.append(args)
            session=ClientSession.__new__(ClientSession)
            session.terminal=t;session.writer=types.SimpleNamespace(deadline=t.deadline)
            session.capture=bytearray();session.drain=lambda:None
            session.view=types.SimpleNamespace(pending=False,cut=0)
            session.enter_cleanup()
            self.assertEqual(session.writer.deadline,t.cleanup_deadline)
            if ordinary:
                with self.assertRaises(ValueError):session.send(b'x','gameplay',lambda:True)
                self.assertEqual(sent,[]);self.assertTrue(t.failed)
            else:
                session.send(b'x','observed cleanup',lambda:True,cleanup=True)
                self.assertEqual(len(sent),1)
                with self.assertRaises(ValueError):session.enter_cleanup()
        t=self.transport({});t.cleanup_deadline=0
        session.terminal=t
        with self.assertRaises(TimeoutError):session.enter_cleanup()
        with self.assertRaises((ValueError,TimeoutError)):session.send(b'x','expired',lambda:True,cleanup=True)
    def test_bad_sequence_poison_snapshot_and_no_key(self):
        for reply in [{'op':'SNAPSHOT','seq':2},{'op':'SNAPSHOT','seq':True},[],{'op':'SNAPSHOT','seq':1,'cursor':{'x':False,'y':0},'lines':['']*25}]:
            with self.subTest(reply=reply):
                t=self.transport(reply)
                with self.assertRaises(ValueError):t.snapshot()
                self.assertTrue(t.failed)
                with self.assertRaises(ValueError):t.key(b'x','forbidden')
    def test_wrong_snapshot_opcode_poison(self):
        t=self.transport({'op':'CLOSED','seq':1})
        with self.assertRaises(ValueError):t.snapshot()
        self.assertTrue(t.failed)
    def test_close_failure_not_marked_joined(self):
        t=self.transport({'op':'WRONG','seq':1})
        with self.assertRaises(ValueError):t.close()
        self.assertFalse(t.closed);self.assertTrue(t.failed)
    def test_full_stalled_pipe_obeys_original_deadline(self):
        t=self.transport({})
        while True:
            try:os.write(t.process.stdin.fileno(),b'x'*4096)
            except BlockingIOError:break
        t.deadline=time.monotonic()+.03
        with self.assertRaises(TimeoutError):t.snapshot()
        self.assertTrue(t.failed)
        self.assertIn('failure',(pathlib.Path(self.folder.name)/'terminal.protocol.jsonl').read_text())
    def test_expired_observation_and_key_do_not_write(self):
        t=self.transport({});t.deadline=0
        with self.assertRaises(TimeoutError):t.snapshot()
        with self.assertRaises(ValueError):t.key(b'x','late')
        os.set_blocking(t.readfd,False)
        with self.assertRaises(BlockingIOError):os.read(t.readfd,10)
    def test_pty_writer_rejects_wrong_size_echo_and_deadline(self):
        writer=PtyWriter(-1,-1,time.monotonic()+1)
        import struct,termios
        with patch('pty_probe.fcntl.ioctl',return_value=struct.pack('HHHH',25,80,0,0)):
            with self.assertRaises(ValueError):writer(b'x','key',1)
        with patch('pty_probe.fcntl.ioctl',return_value=struct.pack('HHHH',25,100,0,0)),patch('pty_probe.termios.tcgetattr',return_value=[0,0,0,termios.ECHO,0,0,[]]):
            with self.assertRaises(ValueError):writer(b'x','key',1)
        writer.deadline=0
        with self.assertRaises(TimeoutError):writer(b'x','key',1)
        self.assertEqual(writer.audit,[])
        for partial in (False,True):
            with self.subTest(partial=partial):
                t=self.transport({});attempts=[]
                def refuse(data,kind,seq):
                    attempts.append(data[:1] if partial else b'')
                    raise OSError('Injected uncertain writer failure')
                t.writer=refuse
                with self.assertRaises(OSError):t.key(b'abc','first')
                self.assertTrue(t.failed)
                with self.assertRaises(ValueError):t.key(b'abc','retry-forbidden')
                with self.assertRaises(ValueError):t.close()
                self.assertEqual(len(attempts),1);self.assertFalse(t.closed)


if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(Controls)
    assert suite.countTestCases()==10
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    summary={'Suite':'terminal-transport','Expected':10,'Executed':result.testsRun,'Failures':len(result.failures),'Errors':len(result.errors),'Skipped':len(result.skipped),'Success':result.wasSuccessful() and result.testsRun==10 and not result.skipped}
    print(json.dumps(summary));raise SystemExit(0 if summary['Success'] else 1)
