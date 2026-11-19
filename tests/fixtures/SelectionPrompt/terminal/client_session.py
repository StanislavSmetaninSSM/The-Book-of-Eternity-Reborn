"""Foreground client transport only. No owner adoption or gameplay authority."""
import os, pathlib, select, sys, time
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent.parent))
from owned_terminal import OwnedTerminal
from pty_probe import PtyWriter
from fullroute_inputs import AcknowledgedTerminalView
from client_ui import main_ready,player_ready

class ClientSession:
    def __init__(self,master,captured,evidence,node,prefix,work_deadline,cleanup_deadline,drain):
        self.writer=PtyWriter(master,master,work_deadline);self.capture=captured;self.drain=drain
        self.terminal=OwnedTerminal(node,prefix,evidence,work_deadline,self.writer,cleanup_deadline=cleanup_deadline)
        self.view=AcknowledgedTerminalView(self.terminal,captured)
    def feed(self,data):self.view.observe(data)
    def main(self,cut,selected=0):return main_ready(self.view,bytes(self.capture),cut,selected)
    def player(self,cut):return player_ready(self.view,bytes(self.capture),cut)
    def send(self,key,label,predicate):
        self.terminal._time();self.drain();self.terminal._time()
        if self.view.pending or self.view.cut!=len(self.capture) or not predicate():raise ValueError('Fresh client input predicate unproved')
        value=key() if callable(key) else key
        cut=len(self.capture)
        self.terminal.key(value,label,cleanup=self.terminal.cleanup_entered)
        return cut
    def enter_cleanup(self):
        self.terminal.enter_cleanup();self.writer.deadline=self.terminal.cleanup_deadline
    def close(self):self.terminal.close()
    def abort(self):return self.terminal.abort()
