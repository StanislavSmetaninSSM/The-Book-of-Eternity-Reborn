"""Pure parser controls, no PTY, process, or terminal authority."""
import unittest
from smoke import Screen

ALPHA=b'> SMOKE_ALPHA\r\n  SMOKE_BETA'
BETA=b'  SMOKE_ALPHA\r\n> SMOKE_BETA'

class ScreenTests(unittest.TestCase):
    def test_redraw_replaces_selection(self):
        s=Screen();s.feed(ALPHA)
        self.assertTrue(s.selected('SMOKE_ALPHA'));self.assertFalse(s.selected('SMOKE_BETA'))
        s.feed(b'\x1b[2J\x1b[H'+BETA)
        self.assertTrue(s.selected('SMOKE_BETA'));self.assertFalse(s.selected('SMOKE_ALPHA'))
        s.feed(b'\x1b[2J')
        self.assertFalse(s.selected('SMOKE_BETA'))
    def test_split_csi_cannot_authorize_stale_selection(self):
        s=Screen();s.feed(BETA)
        s.feed(b'\x1b[')
        self.assertFalse(s.selected('SMOKE_BETA'))
        s.feed(b'2J');s.finish()
        self.assertFalse(s.selected('SMOKE_BETA'))
    def test_split_utf8_completion(self):
        s=Screen();s.feed(BETA)
        s.feed(b'\r\n\xc3')
        self.assertFalse(s.selected('SMOKE_BETA'))
        s.feed(b'\xa9');s.finish()
        self.assertEqual(s.lines()[2],'é')
        self.assertTrue(s.selected('SMOKE_BETA'))
    def test_alternate_buffers_refused(self):
        for code in (b'\x1b[?1049h',b'\x1b[?1049l',b'\x1b[?47h',b'\x1b[?1047h'):
            with self.subTest(code=code):
                s=Screen();s.feed(BETA)
                with self.assertRaises(ValueError):s.feed(code)
                self.assertFalse(s.selected('SMOKE_BETA'))
    def test_conceal_and_unknown_styles_refused(self):
        for code in (b'\x1b[8m',b'\x1b[0;8m',b'\x1b[999m',b'\x1b[?25m'):
            with self.subTest(code=code):
                s=Screen()
                with self.assertRaises(ValueError):s.feed(code+ALPHA)
                self.assertFalse(s.selected('SMOKE_ALPHA'))
    def test_unknown_modes_and_invalid_parameters_refused(self):
        for code in (b'\x1b[4h',b'\x1b[?7l',b'\x1b[?25;1049h',b'\x1b[9K',b'\x1b[1;2A',b'\x1b[?2J'):
            with self.subTest(code=code):
                with self.assertRaises(ValueError):Screen().feed(code)
    def test_incomplete_eof_refused(self):
        for data in (b'\x1b',b'\x1b[',b'\x1b[12;',b'\xc3'):
            with self.subTest(data=data):
                s=Screen();s.feed(BETA+data)
                with self.assertRaises((ValueError,UnicodeError)):s.finish()
                self.assertFalse(s.selected('SMOKE_BETA'))
    def test_supported_visibility_styles_and_no_dsr_reply(self):
        s=Screen();s.feed(b'\x1b[?25l\x1b[1m'+ALPHA+b'\x1b[0m\x1b[?25h');s.finish()
        self.assertTrue(s.selected('SMOKE_ALPHA'))
        with self.assertRaises(RuntimeError):Screen().feed(b'\x1b[6n')

class KeyboardAndFrameTests(unittest.TestCase):
    def test_application_cursor_changes_down_encoding(self):
        s=Screen();self.assertEqual(s.down(),b'\x1b[B')
        s.feed(b'\x1b[?1h\x1b=')
        self.assertEqual(s.down(),b'\x1bOB');self.assertTrue(s.keyboard['keypad'])
        s.feed(b'\x1b[?1l\x1b>')
        self.assertEqual(s.down(),b'\x1b[B');self.assertFalse(s.keyboard['keypad'])
    def test_split_keyboard_mode_and_unknown_escape(self):
        s=Screen();s.feed(b'\x1b[?');s.feed(b'1h\x1b');s.feed(b'=')
        self.assertEqual(s.down(),b'\x1bOB')
        with self.assertRaises(ValueError):s.feed(b'\x1bZ')
    def test_pipe_frame_is_fresh_and_not_echo(self):
        s=Screen();s.feed(b'T024 inert selection\n\n'+ALPHA)
        self.assertTrue(s.frame('SMOKE_ALPHA',0))
        cut=len(s.plain)
        self.assertFalse(s.frame('SMOKE_ALPHA',cut))
        s.feed(b'\r\x1b[3AT024 inert selection\n\n'+BETA)
        self.assertTrue(s.frame('SMOKE_BETA',cut))
        s.feed(b'\rjunk')
        self.assertFalse(s.frame('SMOKE_BETA',cut))

if __name__=='__main__':
    unittest.main()
