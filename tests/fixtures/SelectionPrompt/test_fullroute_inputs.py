"""Bounded DATA controls for the actual full-route input observer. No child/UI."""
import unittest
from unittest.mock import patch
from fullroute_inputs import (OPTIONS, DESCRIPTIONS, OPTIONS_FOOTER, options_frame,
                             ordered_transition, retained_selection, clean_save_interval, require_time, wait_observation, send_current)
import mortal_console_routes as routes


def options(selected, initial=True):
    text = '\x1b[2J\x1b[3J\x1b[1;1H── ⚙️ Игровое меню ──\r\n\r\nEsc — назад\r\n' if initial else '\x1b[5;1H'
    for label, description in zip(OPTIONS, DESCRIPTIONS):
        text += '    ' + ('➤ ' if selected == label else '◆ ') + label + '  \r\n'
        if description:
            text += '       ' + description + '\r\n \r\n'
    return (text + '\r\n  ' + OPTIONS_FOOTER).encode()


class FullRouteInputData(unittest.TestCase):
    def test_coalesced_transition_retains_current_without_replaying_history(self):
        self.assertTrue(ordered_transition(['archive', 'archive', 'Back'], 'archive', 'Back'))
        menu = routes.fixed_menu('Load', ['archive', 'Back'], 15)
        raw = b'Load\n> archive\n  Back\r\x1b[3ALoad\n  archive\n> Back'
        proof = {'raw': raw, 'menu': menu, 'selected': 'Back'}
        self.assertEqual('Back', retained_selection(raw, proof))
        self.assertIsNone(retained_selection(raw + b'\r\x1b[3ALoad\n> ar', proof))
        with self.assertRaises(ValueError):
            retained_selection(raw + b'\r\x1b[3ALoad\n> archive\n  Back', proof)

    def test_transition_refuses_reverse_and_skip(self):
        for frames in (['Back', 'archive'], ['third']):
            with self.assertRaises(ValueError):
                ordered_transition(frames, 'archive', 'Back')
        self.assertFalse(ordered_transition(['archive'], 'archive', 'Back'))

    def test_complete_initial_and_fresh_options_grid(self):
        self.assertTrue(options_frame(options(OPTIONS[0]), 0, OPTIONS[0], initial=True))
        old = options(OPTIONS[0])
        self.assertTrue(options_frame(old + options(OPTIONS[1], False), len(old), OPTIONS[1], initial=False))

    def test_options_refuses_missing_reordered_title_description_footer(self):
        original = options(OPTIONS[0])
        for text in ('⚙️ Игровое меню', 'Esc — назад', DESCRIPTIONS[0], OPTIONS_FOOTER):
            self.assertFalse(options_frame(original.replace(text.encode(), b''), 0, OPTIONS[0], initial=True))
        changed = original.replace(OPTIONS[1].encode(), b'TEMP').replace(OPTIONS[2].encode(), OPTIONS[1].encode()).replace(b'TEMP', OPTIONS[2].encode())
        self.assertFalse(options_frame(changed, 0, OPTIONS[0], initial=True))

    def test_options_refuses_stale_partial_and_late_controls(self):
        old = options(OPTIONS[0])
        for tail in (b' ', options(OPTIONS[0], False)[:-10], options(OPTIONS[0], False) + b'\x1b[2K'):
            self.assertFalse(options_frame(old + tail, len(old), OPTIONS[0], initial=False))

    def test_save_warning_is_checked_through_returned_stream_boundary(self):
        success = '✅ Игра сохранена\n'
        self.assertTrue(clean_save_interval(success + 'inventory'))
        warning = 'Требуется служебная проверка хранилища сохранений.'
        for split in range(len(warning) + 1):
            collected = success + warning[:split] + warning[split:] + '\ninventory'
            self.assertFalse(clean_save_interval(collected))
        self.assertFalse(clean_save_interval(success * 2))

    def test_deadline_equality_and_late_observation_refuse(self):
        require_time(45, 44.999)
        for now in (45, 45.001, 60):
            with self.assertRaises(TimeoutError):
                require_time(45, now)

    def test_real_wait_does_not_accept_expired_or_slow_positive(self):
        calls = []
        with patch('fullroute_inputs.time.monotonic', return_value=45):
            with self.assertRaises(TimeoutError):
                wait_observation(lambda: calls.append('predicate') or True, lambda: None, 45, 'test')
        self.assertEqual([], calls)
        with patch('fullroute_inputs.time.monotonic', side_effect=[44, 45]):
            with self.assertRaises(TimeoutError):
                wait_observation(lambda: True, lambda: None, 45, 'test')

    def test_real_send_never_calls_writer_after_deadline(self):
        calls = []
        with patch('fullroute_inputs.time.monotonic', return_value=45):
            with self.assertRaises(TimeoutError):
                send_current(lambda *args: calls.append(args), b'\r', 'Enter', 45)
        self.assertEqual([], calls)



if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(FullRouteInputData)
    assert suite.countTestCases() == 9
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    assert result.testsRun == 9 and not result.skipped
    raise SystemExit(0 if result.wasSuccessful() else 1)
