"""T027 authored captured-pipe DATA, not retained live bytes or terminal authority."""
import unittest
from mortal_console_routes import selected_frame

TITLE = '🎒 📦 Инвентарь'
HEADER = TITLE + '  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)'

def frame(selected=0):
    labels = ['💰 Деньги: 500', '📦 Мел', '← Назад']
    return (HEADER + '\n' + ' '*75 + '\n' + '\n'.join(
        ('> ' if index == selected else '  ') + label + ' '*8
        for index,label in enumerate(labels))).encode()

class MenuFrameTests(unittest.TestCase):
    def test_complete_padded_inventory_has_no_carriage_return(self):
        raw=frame();self.assertTrue(raw.endswith(b' '));self.assertNotIn(b'\r',raw)
        self.assertEqual(selected_frame(raw,0,TITLE),'💰 Деньги: 500')
    def test_complete_selected_terminal_row(self):
        self.assertEqual(selected_frame(frame(2),0,TITLE),'← Назад')
    def test_truncated_footer_or_row_is_not_complete(self):
        raw=frame();footer=raw.index('← Назад'.encode())
        for cut in (footer,footer+1,footer+len('← На'.encode())):
            with self.subTest(cut=cut):self.assertIsNone(selected_frame(raw[:cut],0,TITLE))
    def test_stale_cut_and_new_incomplete_draw_refuse(self):
        raw=frame()
        self.assertIsNone(selected_frame(raw,len(raw),TITLE))
        self.assertIsNone(selected_frame(raw+b'\n'+(HEADER+'\n> ').encode(),0,TITLE))
    def test_ambiguous_footer_pagination_wrapping_and_trailing_output_refuse(self):
        raw=frame()
        for tail in ('\n  ← Назад','\n(Move up and down to reveal more items)',
                     '\n  wrapped continuation','\nВаш ход\n >  '):
            with self.subTest(tail=tail):self.assertIsNone(selected_frame(raw+tail.encode(),0,TITLE))
        self.assertIsNone(selected_frame(raw.replace('  📦 Мел'.encode(),'> 📦 Мел'.encode()),0,TITLE))
    def test_partial_utf8_and_unsupported_controls_refuse(self):
        for raw in (frame()+b'\xd0',frame()+b'\x1b[',b'\x1b[8m'+frame(),b'\x1b[?1049h'+frame()):
            with self.subTest(raw=raw[-12:]):self.assertIsNone(selected_frame(raw,0,TITLE))
    def test_incidental_title_in_choice_is_not_a_header(self):
        raw=('Other menu\n  '+HEADER+'\n> 💰 Деньги: 500\n  ← Назад        ').encode()
        self.assertIsNone(selected_frame(raw,0,TITLE))

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(MenuFrameTests)
    if suite.countTestCases()!=7:raise SystemExit('Expected exactly seven menu-frame DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==7 and result.wasSuccessful() and not result.skipped else 1)
