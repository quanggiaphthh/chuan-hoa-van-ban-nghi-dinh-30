import unittest

from correct_v25_l4c_test_default import correct_source


class CorrectV25L4CTestDefaultTests(unittest.TestCase):
    def test_corrective_is_repeatable(self):
        source = """
static string Doc(W.JustificationValues? a=W.JustificationValues.Left,bool prot=false){}
var s=Doc(null);var o=s+\".o\";
"""
        expected = """
static string Doc(W.JustificationValues? a=null,bool prot=false,bool missingDirect=false){if(a is null&&!missingDirect)a=W.JustificationValues.Left;}
var s=Doc(null,missingDirect:true);var o=s+\".o\";
"""

        corrected = correct_source(source)

        self.assertEqual(expected, corrected)
        self.assertEqual(corrected, correct_source(corrected))

    def test_corrective_rejects_unrecognized_source(self):
        with self.assertRaises(SystemExit):
            correct_source("unrecognized materializer source")


if __name__ == "__main__":
    unittest.main()
