import unittest

from apply_v23_runtime_corrections import correct_fixture_generator


class V23RuntimeCorrectionTests(unittest.TestCase):
    def test_generator_uses_fixed_metadata_for_every_fixture_part(self):
        source = """from zipfile import ZipFile, ZIP_DEFLATED
CT='http://schemas.openxmlformats.org/package/2006/content-types'
z.writestr('[Content_Types].xml',types)
z.writestr('_rels/.rels',rootrels)
z.writestr('word/document.xml',document)
z.writestr('word/styles.xml',styles)
z.writestr('word/settings.xml',settings)
z.writestr('word/_rels/document.xml.rels',rels)
"""

        corrected = correct_fixture_generator(source)

        self.assertIn('ZIP_TIME=(1980,1,1,0,0,0)', corrected)
        self.assertEqual(6, corrected.count('add_entry(z,'))
        self.assertNotIn('z.writestr(', corrected)
        self.assertEqual(corrected, correct_fixture_generator(corrected))

    def test_generator_correction_rejects_missing_or_ambiguous_source(self):
        with self.assertRaises(SystemExit):
            correct_fixture_generator('unrecognized source')
        with self.assertRaises(SystemExit):
            correct_fixture_generator(
                "from zipfile import ZipFile, ZIP_DEFLATED\n"
                "CT='http://schemas.openxmlformats.org/package/2006/content-types'\n"
                "CT='http://schemas.openxmlformats.org/package/2006/content-types'\n"
            )


if __name__ == '__main__':
    unittest.main()
