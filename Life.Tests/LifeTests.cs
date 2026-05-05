using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Life;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Life.Tests
{
    [TestClass]
    public class CellTests
    {
        [TestMethod]
        public void Cell_DefaultState_IsDead()
        {
            var cell = new Cell();
            Assert.IsFalse(cell.IsAlive);
        }

        [TestMethod]
        public void Cell_InitAlive_IsAlive()
        {
            var cell = new Cell(true);
            Assert.IsTrue(cell.IsAlive);
        }

        [TestMethod]
        public void Cell_Clone_ReturnsIndependentCopy()
        {
            var cell = new Cell(true);
            var clone = cell.Clone();
            clone.IsAlive = false;
            Assert.IsTrue(cell.IsAlive);
        }

        [TestMethod]
        public void Cell_ToString_AlivePrintsSquare()
        {
            var cell = new Cell(true);
            Assert.AreEqual("■", cell.ToString());
        }

        [TestMethod]
        public void Cell_ToString_DeadPrintsEmpty()
        {
            var cell = new Cell(false);
            Assert.AreEqual("□", cell.ToString());
        }
    }

    [TestClass]
    public class BoardTests
    {
        // ── Construction ──────────────────────────────────────────────
        [TestMethod]
        public void Board_NewBoard_AllCellsDead()
        {
            var b = new Board(5, 5);
            Assert.AreEqual(0, b.CountAlive());
        }

        [TestMethod]
        public void Board_NewBoard_GenerationZero()
        {
            var b = new Board(5, 5);
            Assert.AreEqual(0, b.Generation);
        }

        // ── SetAlive / IsAlive ────────────────────────────────────────
        [TestMethod]
        public void Board_SetAlive_CellBecomesAlive()
        {
            var b = new Board(5, 5);
            b.SetAlive(2, 2, true);
            Assert.IsTrue(b.IsAlive(2, 2));
        }

        [TestMethod]
        public void Board_SetAlive_CountAliveUpdates()
        {
            var b = new Board(5, 5);
            b.SetAlive(1, 1, true);
            b.SetAlive(2, 2, true);
            Assert.AreEqual(2, b.CountAlive());
        }

        // ── Rules ─────────────────────────────────────────────────────
        [TestMethod]
        public void Board_Step_BlockStaysStable()
        {
            // 2x2 block is stable
            var b = new Board(6, 6);
            b.SetAlive(2, 2, true); b.SetAlive(2, 3, true);
            b.SetAlive(3, 2, true); b.SetAlive(3, 3, true);
            int before = b.CountAlive();
            b.Step();
            Assert.AreEqual(before, b.CountAlive());
        }

        [TestMethod]
        public void Board_Step_BlinkerOscillates()
        {
            // Horizontal blinker
            var b = new Board(5, 5);
            b.SetAlive(2, 1, true); b.SetAlive(2, 2, true); b.SetAlive(2, 3, true);
            b.Step();
            // After 1 step: vertical blinker — cells (1,2),(2,2),(3,2) alive
            Assert.IsTrue(b.IsAlive(1, 2));
            Assert.IsTrue(b.IsAlive(2, 2));
            Assert.IsTrue(b.IsAlive(3, 2));
            Assert.IsFalse(b.IsAlive(2, 1));
            Assert.IsFalse(b.IsAlive(2, 3));
        }

        [TestMethod]
        public void Board_Step_BlinkerReturnsToOriginAfter2Steps()
        {
            var b = new Board(5, 5);
            b.SetAlive(2, 1, true); b.SetAlive(2, 2, true); b.SetAlive(2, 3, true);
            b.Step(); b.Step();
            Assert.IsTrue(b.IsAlive(2, 1));
            Assert.IsTrue(b.IsAlive(2, 2));
            Assert.IsTrue(b.IsAlive(2, 3));
        }

        [TestMethod]
        public void Board_Step_LonelyCell_Dies()
        {
            var b = new Board(5, 5);
            b.SetAlive(2, 2, true);
            b.Step();
            Assert.IsFalse(b.IsAlive(2, 2));
        }

        [TestMethod]
        public void Board_Step_Overpopulated_Dies()
        {
            // 5 neighbors → cell dies
            var b = new Board(5, 5);
            // surround center with 5 alive cells
            b.SetAlive(2, 2, true); // center
            b.SetAlive(1, 1, true); b.SetAlive(1, 2, true); b.SetAlive(1, 3, true);
            b.SetAlive(2, 1, true); b.SetAlive(2, 3, true);
            b.Step();
            Assert.IsFalse(b.IsAlive(2, 2));
        }

        [TestMethod]
        public void Board_Step_ThreeNeighbors_DeadCellBorn()
        {
            var b = new Board(5, 5);
            b.SetAlive(1, 1, true); b.SetAlive(1, 2, true); b.SetAlive(2, 1, true);
            b.Step();
            Assert.IsTrue(b.IsAlive(2, 2)); // born
        }

        [TestMethod]
        public void Board_Step_IncreasesGeneration()
        {
            var b = new Board(5, 5);
            b.Step();
            Assert.AreEqual(1, b.Generation);
        }

        // ── Randomize ─────────────────────────────────────────────────
        [TestMethod]
        public void Board_Randomize_DensityZero_NoCellsAlive()
        {
            var b = new Board(10, 10);
            b.Randomize(0.0);
            Assert.AreEqual(0, b.CountAlive());
        }

        [TestMethod]
        public void Board_Randomize_DensityOne_AllCellsAlive()
        {
            var b = new Board(10, 10);
            b.Randomize(1.0);
            Assert.AreEqual(100, b.CountAlive());
        }

        [TestMethod]
        public void Board_Randomize_ResetsGeneration()
        {
            var b = new Board(5, 5);
            b.Step(); b.Step();
            b.Randomize(0.5);
            Assert.AreEqual(0, b.Generation);
        }

        // ── Save / Load ───────────────────────────────────────────────
        [TestMethod]
        public void Board_SaveLoad_PreservesState()
        {
            var b = new Board(5, 5);
            b.SetAlive(1, 1, true); b.SetAlive(3, 3, true);
            var path = Path.Combine(Path.GetTempPath(), "life_test.txt");
            b.SaveToFile(path);
            var loaded = Board.LoadFromFile(path);
            Assert.IsTrue(loaded.IsAlive(1, 1));
            Assert.IsTrue(loaded.IsAlive(3, 3));
            Assert.IsFalse(loaded.IsAlive(0, 0));
            File.Delete(path);
        }

        [TestMethod]
        public void Board_SaveLoad_PreservesGeneration()
        {
            var b = new Board(5, 5);
            b.SetAlive(2, 2, true);
            b.Step(); b.Step();
            var path = Path.Combine(Path.GetTempPath(), "life_gen.txt");
            b.SaveToFile(path);
            var loaded = Board.LoadFromFile(path);
            Assert.AreEqual(b.Generation, loaded.Generation);
            File.Delete(path);
        }
    }

    [TestClass]
    public class ElementTests
    {
        private static Board SingleCluster(params (int, int)[] cells)
        {
            var b = new Board(10, 10);
            foreach (var (r, c) in cells) b.SetAlive(r, c, true);
            return b;
        }

        [TestMethod]
        public void FindElements_BlockClassifiedAsBlock()
        {
            var b = SingleCluster((4,4),(4,5),(5,4),(5,5));
            var els = b.FindElements();
            Assert.AreEqual(1, els.Count);
            Assert.AreEqual("Block", els[0].Classify());
        }

        [TestMethod]
        public void FindElements_BeehiveClassified()
        {
            var b = SingleCluster((1,2),(1,3),(2,1),(2,4),(3,2),(3,3));
            var els = b.FindElements();
            Assert.AreEqual(1, els.Count);
            Assert.AreEqual("Beehive", els[0].Classify());
        }

        [TestMethod]
        public void FindElements_TwoSeparateClusters()
        {
            var b = new Board(10, 10);
            b.SetAlive(1, 1, true);
            b.SetAlive(8, 8, true);
            var els = b.FindElements();
            Assert.AreEqual(2, els.Count);
        }

        [TestMethod]
        public void FindElements_EmptyBoard_NoElements()
        {
            var b = new Board(10, 10);
            Assert.AreEqual(0, b.FindElements().Count);
        }

        [TestMethod]
        public void FindElements_SingletonClassified()
        {
            var b = SingleCluster((5, 5));
            var els = b.FindElements();
            Assert.AreEqual("Singleton", els[0].Classify());
        }
    }

    [TestClass]
    public class ResearchTests
    {
        [TestMethod]
        public void Research_TrialRunsWithoutException()
        {
            int gen = Research.RunTrial(10, 10, 0.3, 100, 5);
            Assert.IsTrue(gen > 0 && gen <= 100);
        }

        [TestMethod]
        public void Research_ExperimentReturnsDensityPoints()
        {
            var results = Research.RunExperiment(10, 10, 3, 50, 5);
            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.Density > 0 && r.Density < 1));
        }

        [TestMethod]
        public void Research_AnalyseBoardReturnsNonEmpty()
        {
            var b = new Board(10, 10);
            b.Randomize(0.3);
            var report = Research.AnalyseBoard(b);
            Assert.IsTrue(report.Length > 0);
        }
    }

    [TestClass]
    public class SettingsTests
    {
        [TestMethod]
        public void Settings_DefaultValues_AreReasonable()
        {
            var s = new Settings();
            Assert.IsTrue(s.Rows > 0);
            Assert.IsTrue(s.Cols > 0);
            Assert.IsTrue(s.InitialDensity > 0 && s.InitialDensity < 1);
        }

        [TestMethod]
        public void Settings_SaveLoad_PreservesValues()
        {
            var path = Path.Combine(Path.GetTempPath(), "life_settings.json");
            var s = new Settings { Rows = 15, Cols = 30, DelayMs = 200 };
            s.Save(path);
            var loaded = Settings.Load(path);
            Assert.AreEqual(15, loaded.Rows);
            Assert.AreEqual(30, loaded.Cols);
            Assert.AreEqual(200, loaded.DelayMs);
            File.Delete(path);
        }
    }

    [TestClass]
    public class PresetTests
    {
        [TestMethod]
        public void Presets_Glider_HasFiveCells()
        {
            var b = Presets.Glider();
            Assert.AreEqual(5, b.CountAlive());
        }

        [TestMethod]
        public void Presets_Blinker_HasThreeCells()
        {
            var b = Presets.Blinker();
            Assert.AreEqual(3, b.CountAlive());
        }

        [TestMethod]
        public void Presets_Block_HasFourCells()
        {
            var b = Presets.Block();
            Assert.AreEqual(4, b.CountAlive());
        }
    }
}
