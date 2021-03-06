using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecursionTracker.Plugins.VoicepackCombiner.Voicepack;
using RecursionTracker.Plugins.VoicepackCombiner.VoicepackCombinerTest;

namespace RecursionTracker.Plugins.VoicepackCombiner.VoicepackCombinerTest.Voicepack
{
    /// <summary>
    /// Tests through VoicepackExtended rather than VoicepackComparer as it started there, but then 
    /// got delegated to VoicepackComparer which is also tested through these tests. (not sure what
    /// the best practices are in this case)
    /// </summary>
    [TestClass]
    public class EqualComponentInfoTest
    {
        readonly VoicepackExtended _testPack1 = new VoicepackExtended();
        readonly VoicepackExtended _testPack2 = new VoicepackExtended();

        [TestMethod]
        public void NullVoicepacksHaveEqualComponentInformation()
        {
            Assert.IsTrue(_testPack1.EqualComponentInfo(_testPack2));
        }

        [TestMethod]
        public void DefaultVoicepackHasDifferentCompInfoThanNullVoicepack()
        {
            _testPack1.InitializeToDefault();
            Assert.IsFalse(_testPack1.EqualComponentInfo(_testPack2));
        }

        [TestMethod]
        public void IdenticalSampleVoicepacksHaveEqualComponentInformation()
        {
            _testPack1.InitializeToDefault();
            _testPack1.Voicepack.componentInformation = VoicepackSampleCreator.CreateSampleCompInfo();
            _testPack2.InitializeToDefault();
            _testPack2.Voicepack.componentInformation = VoicepackSampleCreator.CreateSampleCompInfo();

            Assert.IsTrue(_testPack1.EqualComponentInfo(_testPack2));
        }

        [TestMethod]
        public void SlightlyDifferentSampleVoicepacksDontHaveEqualComponentInformation()
        {
            _testPack1.InitializeToDefault();
            _testPack1.Voicepack.componentInformation = VoicepackSampleCreator.CreateSampleCompInfo();
            _testPack2.InitializeToDefault();
            _testPack2.Voicepack.componentInformation = VoicepackSampleCreator.CreateSampleCompInfo();
            _testPack2.Voicepack.componentInformation.author = "Someone Else";
            
            Assert.IsFalse(_testPack1.EqualComponentInfo(_testPack2));
        }

        [TestCategory("Test With File Access"), TestMethod]
        public void TestsWithVoicepackFiles()
        {
            var testPack1 = new VoicepackExtended();
            var testPack2 = new VoicepackExtended();

            testPack1.LoadFromFile(TestData.Pack1);
            testPack2.LoadFromFile(TestData.Pack1);
            Assert.IsTrue(testPack2.EqualComponentInfo(testPack1), "Voicepacks loaded from same file should have same info");

            testPack2.LoadFromFile(TestData.VoicepackPAKBased);
            Assert.IsFalse(testPack2.EqualComponentInfo(testPack1), "Voicepacks loaded from different file should not have same info");

            testPack2.LoadFromFile(TestData.Pack1Republished);
            Assert.IsTrue(testPack2.EqualComponentInfo(testPack1), "Voicepacks loaded from republished file should have same info");
        }
    }

    /// <summary>
    /// Tests through VoicepackExtended rather than VoicepackComparer as it started there, but then 
    /// got delegated to VoicepackComparer which is also tested through these tests. In voicepackcomparer this is called
    /// EqualAchievementList 
    /// </summary>
    [TestClass]
    public class EqualSoundFileNamesTest
    {
        readonly VoicepackExtended _testPack1 = new VoicepackExtended();
        readonly VoicepackExtended _testPack2 = new VoicepackExtended();

        [TestMethod]
        public void NullVoicepacksHaveEqualSoundFileNames()
        {
            Assert.IsTrue(_testPack1.EqualSoundFilenames(_testPack2));
        }

        [TestMethod]
        public void DefaultVoicepacksHaveEqualSoundFileNames()
        { 
            _testPack1.InitializeToDefault();
            _testPack2.InitializeToDefault();
            Assert.IsTrue(_testPack1.EqualSoundFilenames(_testPack2));
        }

        [TestMethod]
        public void TwoIdenticalSampleVoicepacksHaveEqualSoundFileNames()
        {
            var samplePack1 = VoicepackSampleCreator.Create();
            var samplePack2 = VoicepackSampleCreator.Create();
            
            Assert.IsTrue(samplePack2.EqualSoundFilenames(samplePack1));
        }

        [TestMethod]
        public void SlightlyDifferentSampleVoicepacksDontHaveEqualSoundFileNames()
        {
            var samplePack1 = VoicepackSampleCreator.Create();
            var samplePack2 = VoicepackSampleCreator.Create();
            samplePack2.Voicepack.groupManager.achievementList[VoicepackSampleCreator.SampleAchievements[1]]
                .dynamicSounds.sounds[0].pakSoundFile = "different sound filename";

            Assert.IsFalse(samplePack2.EqualSoundFilenames(samplePack1));
        }

        [TestCategory("Test With File Access"), TestMethod]
        public void TestsWithVoicepacksfromFile()
        {
            var k = new VoicepackExtended();
            var l = new VoicepackExtended();

            k.LoadFromFile(TestData.Pack1);
            l.LoadFromFile(TestData.Pack1);
            Assert.IsTrue(k.EqualSoundFilenames(l), "Voicepack objects loaded from the same file should be equal");
            Assert.IsTrue(l.EqualSoundFilenames(k), "Voicepack objects loaded from the same file should be equal and commutative");

            l.LoadFromFile(TestData.Pack2);
            Assert.IsFalse(l.EqualSoundFilenames(k), "Voicepacks loaded from different files with different content should not be equal");
            Assert.IsFalse(k.EqualSoundFilenames(l), "Voicepacks loaded from different files with different content should not be equal");

            l.LoadFromFile(TestData.Pack1Republished);
            Assert.IsFalse(k.EqualSoundFilenames(l), "This particular republished voicepack differs in one place, because republishing discards the old one sound when there are dynamic sounds (contrived example, tho not intended)");
        }
    }

    [TestClass]
    public class TestEqualComponentData
    {
        readonly XmlDictionary<string, ComponentData> _compData1 = new XmlDictionary<string, ComponentData>();
        readonly XmlDictionary<string, ComponentData> _compData2 = new XmlDictionary<string, ComponentData>();

        [TestMethod]
        public void NullComponentDataIsEqual()
        {
            Assert.IsTrue(VoicepackComparer.EqualComponentData(null, null));
        }

        [TestMethod]
        public void NullAndEmptyDataDictAreNotEqual()
        {
            Assert.IsFalse(VoicepackComparer.EqualComponentData(null, _compData1));
        }

        [TestMethod]
        public void EmptyComponentDataDictsAreEqual()
        {
            Assert.IsTrue(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }

        [TestMethod]
        public void DataDictWithDifferentSizeAreNotEqual()
        {
            _compData1["key"] = new ComponentData();

            Assert.IsFalse(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }

        [TestMethod]
        public void DataDictWithSameSizeButDifferentKeysAreNotEqual()
        {
            _compData1["key"] = new ComponentData();
            _compData2["otherKey"] = new ComponentData();

            Assert.IsFalse(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }

        [TestMethod]
        public void DataDictsWithSameKeyAndEmptyDataItemAreEqual()
        {
            _compData1["key"] = new ComponentData();
            _compData2["key"] = new ComponentData();

            Assert.IsTrue(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }

        [TestMethod]
        public void DataDicsWithSameKeyButDifferentDataItemSizesAreNotEqual()
        {
            const int dataItemsSize = 5;
            _compData1["key"] = new ComponentData() {data = new byte[dataItemsSize]};
            _compData2["key"] = new ComponentData() {data = new byte[dataItemsSize + 1]};

            Assert.IsFalse(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }

        [TestMethod]
        public void DataDicsWithSameKeyAndSameDataItemSizesAreEqual()
        {
            const int dataItemsSize = 5;
            _compData1["key"] = new ComponentData() {data = new byte[dataItemsSize]};
            _compData2["key"] = new ComponentData() {data = new byte[dataItemsSize]};

            Assert.IsTrue(VoicepackComparer.EqualComponentData(_compData1, _compData2));
        }
    }
}