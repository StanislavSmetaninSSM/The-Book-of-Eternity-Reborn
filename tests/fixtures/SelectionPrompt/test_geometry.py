"""T034 inert descriptor controls using the maintained raw-frame grammar."""
import unittest
from smoke import inert_geometry_selected
ROWS=('s | Ход 43 | Хозяйственный двор у нижней лестницы | 18.11.2026 18:00 | 99999KB','↩️ Назад')
MENU={'header':'Выберите сохранение:','choices':ROWS,'page_size':15}
HEADER='Выберите сохранение:\n'
class GeometryTests(unittest.TestCase):
    def test_padded_complete_rows(self):
        for selected in (0,1):
            body='  \n\n'+'\n'.join(('> ' if i==selected else '  ')+row+'   ' for i,row in enumerate(ROWS))
            self.assertEqual(inert_geometry_selected((HEADER+body).encode(),0,MENU),ROWS[selected])
    def test_wrapped_continuation_is_not_joined(self):
        body='> '+ROWS[0][:65]+'\n  '+ROWS[0][65:]+'\n  '+ROWS[1]
        self.assertIsNone(inert_geometry_selected((HEADER+body).encode(),0,MENU))
    def test_partial_or_control_tail_never_falls_back(self):
        good=(HEADER+'> '+ROWS[0]+'\n  '+ROWS[1]).encode()
        for tail in (b'\n',b'\x1b[2J',b'\x1b[1A',b'\x1b[',b'\n'+HEADER.encode()+b'> s'):
            self.assertIsNone(inert_geometry_selected(good+tail,0,MENU))

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(GeometryTests)
    assert suite.countTestCases()==3
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() and result.testsRun==3 and not result.skipped else 1)
