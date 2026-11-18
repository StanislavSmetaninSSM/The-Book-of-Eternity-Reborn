"""T027 authored captured-pipe DATA, not retained live bytes or terminal authority."""
import unittest
from mortal_console_routes import selected_frame, fixed_menu, inventory_menu, local_load_menu, preflight_local_route

TITLE = '🎒 📦 Инвентарь'
HEADER = TITLE + '  (⚖ 1.8kg/45.0kg кг)  (выберите для просмотра / управления)'

MENU = fixed_menu(HEADER,['💰 Деньги: 500','📦 Мел','← Назад'],20)

def frame(selected=0):
    labels = ['💰 Деньги: 500', '📦 Мел', '← Назад']
    return (HEADER + '\n' + ' '*75 + '\n' + '\n'.join(
        ('> ' if index == selected else '  ') + label + ' '*8
        for index,label in enumerate(labels))).encode()

class MenuFrameTests(unittest.TestCase):
    def test_complete_padded_inventory_has_no_carriage_return(self):
        raw=frame();self.assertTrue(raw.endswith(b' '));self.assertNotIn(b'\r',raw)
        self.assertEqual(selected_frame(raw,0,MENU),'💰 Деньги: 500')
    def test_complete_selected_terminal_row(self):
        self.assertEqual(selected_frame(frame(2),0,MENU),'← Назад')
    def test_truncated_footer_or_row_is_not_complete(self):
        raw=frame();footer=raw.index('← Назад'.encode())
        for cut in (footer,footer+1,footer+len('← На'.encode())):
            with self.subTest(cut=cut):self.assertIsNone(selected_frame(raw[:cut],0,MENU))
    def test_stale_cut_and_new_incomplete_draw_refuse(self):
        raw=frame()
        self.assertIsNone(selected_frame(raw,len(raw),MENU))
        self.assertIsNone(selected_frame(raw+b'\n'+(HEADER+'\n> ').encode(),0,MENU))
    def test_ambiguous_footer_pagination_wrapping_and_trailing_output_refuse(self):
        raw=frame()
        for tail in ('\n  ← Назад','\n(Move up and down to reveal more items)',
                     '\n  wrapped continuation','\nВаш ход\n >  '):
            with self.subTest(tail=tail):self.assertIsNone(selected_frame(raw+tail.encode(),0,MENU))
        self.assertIsNone(selected_frame(raw.replace('  📦 Мел'.encode(),'> 📦 Мел'.encode()),0,MENU))
    def test_partial_utf8_and_unsupported_controls_refuse(self):
        for raw in (frame()+b'\xd0',frame()+b'\x1b[',b'\x1b[8m'+frame(),b'\x1b[?1049h'+frame()):
            with self.subTest(raw=raw[-12:]):self.assertIsNone(selected_frame(raw,0,MENU))
    def test_incidental_title_in_choice_is_not_a_header(self):
        raw=('Other menu\n  '+HEADER+'\n> 💰 Деньги: 500\n  ← Назад        ').encode()
        self.assertIsNone(selected_frame(raw,0,MENU))

    def test_page_fit_and_footer_prefix_collisions_refuse_before_input(self):
        for choices,page in [(['A','Back'],1),(['Back extra','Back'],20),(['B','Back'],20),(['A','A'],20)]:
            with self.subTest(choices=choices),self.assertRaises(ValueError):fixed_menu('Menu',choices,page)
    def test_exact_sequence_rejects_missing_extra_reordered_or_changed_rows(self):
        raw=frame()
        for bad in (raw.replace('📦 Мел'.encode(),'📦 Мел extra'.encode()),
                    raw.replace(('  📦 Мел'+' '*8+'\n').encode(),b''),
                    raw.replace('  📦 Мел'.encode(),'  ← Назад'.encode())):
            with self.subTest(bad=bad):self.assertIsNone(selected_frame(bad,0,MENU))
    def test_source_headers_and_selected_cancel_or_load_back(self):
        for header,choices,page in [('📦 Council\n  Предметов внутри: 1',['Retrieve','← Назад к инвентарю'],10),
                                   ('Выберите предмет для извлечения из хранилища:',['Chalk','← Отмена'],20),
                                   ('Выберите сохранение:',['Save | Ход 42','↩️ Назад'],15)]:
            menu=fixed_menu(header,choices,page)
            raw=(header+'\n  '+choices[0]+'   \n> '+choices[1]+'   ').encode()
            self.assertEqual(selected_frame(raw,0,menu),choices[1])
            self.assertIsNone(selected_frame(b'incidental '+raw,0,menu))
            self.assertIsNone(selected_frame(raw.replace(header.encode(),(header+' extra').encode()),0,menu))
    def test_all_byte_prefixes_before_complete_footer_refuse(self):
        raw=frame();end=raw.index('← Назад'.encode())+len('← Назад'.encode())
        for cut in range(end):
            with self.subTest(cut=cut):self.assertIsNone(selected_frame(raw[:cut],0,MENU))
        self.assertEqual(selected_frame(raw[:end],0,MENU),'💰 Деньги: 500')
    def test_inventory_contract_uses_fresh_labels_counts_and_weight(self):
        inv={'items':[{'itemId':'i','name':'Мел','type':'Материал'}],'equippedItems':{},'totalWeight':'1kg','maxWeight':'45kg'}
        loc={'locationStorages':[{'name':'Council','hasFullAccess':False,'contents':[]}]}
        menu=inventory_menu(inv,loc,{'money':500})
        self.assertEqual(menu['choices'],('💰 Деньги: 500','📦 Мел [Материал]','📦 🔒 Council (0 пр.)','← Назад'))
        self.assertIn('(⚖ 1kg/45kg кг)',menu['header'])
        with self.assertRaises(ValueError):inventory_menu(dict(inv,items=inv['items']*21),loc,{'money':500})
        with self.assertRaises(ValueError):fixed_menu('Menu',['A'*79,'Back'],20)

    def test_actual_redraw_prefix_coalesced_and_fresh_cut(self):
        redraw=b'\r\x1b[4A'
        self.assertEqual(selected_frame(frame()+redraw+frame(2),0,MENU),'← Назад')
        self.assertEqual(selected_frame(redraw+frame(2),0,MENU),'← Назад')
        for tail in (b'',HEADER.encode(),(HEADER+'\n> ').encode()):
            self.assertIsNone(selected_frame(frame()+redraw+tail,0,MENU))
        two=fixed_menu(HEADER,['💰 Деньги: 500','← Назад'],20)
        draw=(HEADER+'\n\n> 💰 Деньги: 500   \n  ← Назад   ').encode()
        self.assertEqual(selected_frame(draw+b'\r\x1b[3A'+draw,0,two),'💰 Деньги: 500')
    def test_cursor_erase_and_wrong_redraw_height_refuse(self):
        for control in (b'\x1b[2K',b'\x1b[2J',b'\x1b[1A',b'\x1b[3;1A',b'\x1b[?25h'):
            self.assertIsNone(selected_frame(frame()+control,0,MENU))
            self.assertIsNone(selected_frame(frame().replace('Мел'.encode(),control+'Мел'.encode()),0,MENU))
        self.assertIsNone(selected_frame(frame()+b'\r\x1b[3A'+frame(),0,MENU))
    def test_long_location_load_supported_label_and_oversize_refuse(self):
        menu=local_load_menu('Хозяйственный двор у кладовой совета',43,99999)
        self.assertTrue(menu['choices'][0].startswith('s | Ход 43 | '))
        with self.assertRaises(ValueError):local_load_menu('Хозяйственный двор у кладовой совета',43,100000)
        with self.assertRaises(ValueError):local_load_menu('Location '*20,43,1)
    def test_whole_local_preflight_rejects_picker_page_or_prefix_before_actions(self):
        inv={'items':[{'itemId':'i','name':'Мел','type':'Материал'}],'equippedItems':{}}
        loc={'name':'Court','locationStorages':[{'storageId':'s','name':'Council','hasFullAccess':True,'contents':[{'itemId':'target','name':'Rope','type':'Материал'}]}]}
        preflight_local_route(inv,loc,{'money':0},'s','target',43)
        self.assertEqual(len(inv['items']),1);self.assertEqual(len(loc['locationStorages'][0]['contents']),1)
        loc['locationStorages'][0]['contents'][0]['name']='← Отмена extra'
        with self.assertRaises(ValueError):preflight_local_route(inv,loc,{'money':0},'s','target',43)

if __name__=='__main__':
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(MenuFrameTests)
    if suite.countTestCases()!=16:raise SystemExit('Expected exactly sixteen menu-frame DATA methods')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.testsRun==16 and result.wasSuccessful() and not result.skipped else 1)
