using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using RecursionTracker.Plugins.PlanetSide2;

namespace RecursionTracker.Plugins.VoicePackCombiner
{
    //using GlobalVariablesPS2 = RecursionTracker.Plugins.PlanetSide2.GlobalVariablesPS2;
    //Shorthand type alias
    using AchievementList = XmlDictionary<string, AchievementOptions>;

    //public class AchievementList : XmlDictionary<string, AchievementOptions>
    //{

    //}


    /// <summary>
    /// This class represents an extension to AchievementOptionsComponents (which is the in memory representation of VoicePacks)
    /// When loading/saving voicepack files from/to a file using AchievementOptionsComponents,
    /// some global variables get written. This is a wrapper class to try to decouple the loading of the voicepack
    /// from a file, from the actual using of the voicepack by the main program.
    /// 
    /// It also allows for merging of AchievementOptionsComponents (TODO:put this functionality in its own class)
    /// 
    /// </summary>
    public class VoicePackExtended
    {
        /// Global Variables to take care/keep track of:
        /// GlobalVariablesPS2.achievementOptions: the instance that gets used by the main program, gets changed during loading/saving
        /// GlobalVariablesPS2.loadedVoicePack: gets set when loading PAK file, is set to null when loadedVoicePack() is called
        /// GlobalVariablesPS2.loadedVoicePackConfigFile: I think this would point to a non-PAK (xml based) voice pack
        /// GlobalVariablesPS2.usingPAK: boolean that indicates if the voicepack is loaded from a pak file, or a xml based voice pack

        public AchievementOptionsComponents VoicePack { get; set; } = null;
        public string LoadedVoicePack { get; set; }
        public string LoadedVoicePackConfigFile { get; set; }
        public bool UsingPAK { get; set; }

        /// <summary>
        /// Creates a default soundpack from null
        /// </summary>
        public void InitializeToDefault()
        {
            VoicePack = new AchievementOptionsComponents();
            VoicePack.InitializeOnNull(); //reservers memory for all composite objects
            //VoicePack.LoadNewAchievements(); //Creates the achievement list TODO: restoreDefaults does too, though it doesnt check if BasicDynamicSoundManager is created, all tests pass without it.. properly check!
            VoicePack.RestoreDefaults(); //Adds componentInformation
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        //public VoicePackExtended(VoicePackExtended other)
        //{
        //    this.LoadedVoicePack = other.LoadedVoicePack;
        //    this.LoadedVoicePackConfigFile = other.LoadedVoicePackConfigFile;
        //    this.UsingPAK = other.UsingPAK;
        //}

        /// <summary>
        /// Creates a reference to the global AchievementOptions and all of the global state affected by loading/saving
        /// the achievementOptions
        /// </summary>
        public void GetFromGlobal()
        {
            VoicePack = GlobalVariablesPS2.achievementOptions;
            LoadedVoicePack = GlobalVariablesPS2.loadedVoicePack;
            LoadedVoicePackConfigFile = GlobalVariablesPS2.loadedVoicePackConfigFile;
            UsingPAK = GlobalVariablesPS2.usingPAK;
        }

        /// <summary>
        /// Sets this voicepack as the GlobalVariablesPS2.achievementOptions, which causes this.VoicePack to be used by the main program.
        /// </summary>
        public void SetAsGlobal()
        {
            //this test prevents my unittests from working TODO: find solution or is it actually useful? maybe delete
            //if (!IsValidVoicePackLoaded())
            //    throw new InvalidOperationException("achievementOptions not loaded/initialized");

            GlobalVariablesPS2.achievementOptions = VoicePack;
            GlobalVariablesPS2.loadedVoicePack = LoadedVoicePack;
            GlobalVariablesPS2.loadedVoicePackConfigFile = LoadedVoicePackConfigFile;
            GlobalVariablesPS2.usingPAK = UsingPAK;
        }

        /// <summary>
        /// Checks if this voicepack is loaded as global voicepack, aka is in use by the main program
        /// </summary>
        /// <remarks> 
        /// When the user loads a new voicepack via the main program, one of the loadedpak strings should be different,
        /// while the achievementOptions reference stays the same.
        /// Its ok if the voicepack content is changed in the main program, its still the same voicepack.
        /// </remarks>
        public bool IsGlobal()
        {
            return GlobalVariablesPS2.loadedVoicePack == this.LoadedVoicePack
                   && GlobalVariablesPS2.loadedVoicePackConfigFile == this.LoadedVoicePackConfigFile;
        }


        /// <summary>
        /// Loads the voicepack from file into this instance. Restores any global state that is altered during this operation.
        /// </summary>
        /// <remarks> 
        /// the function achievementOptions.LoadFromPAKFile(string) will always load itself into the global instance, thats why we need
        /// the workaround in this function to load it in (move it in) a seperate instance.
        /// </remarks>
        public bool LoadFromFile(string filename)
        {
            var globalBackup = new VoicePackExtended();

            globalBackup.GetFromGlobal();
            {
                GlobalVariablesPS2.achievementOptions = new AchievementOptionsComponents();
                GlobalVariablesPS2.achievementOptions.LoadFromPAKFile(filename);
                this.GetFromGlobal();
            }
            globalBackup.SetAsGlobal();

            return IsValidVoicePackLoaded();
        }

        /// <summary>
        /// Returns true if this contains a valid voicepack, i.e. a voicepack that contains something rather than nothing
        /// </summary>
        /// <returns></returns>
        public bool IsValidVoicePackLoaded()
        {
            //This is not a complete validation, but trail and error has shown this to be enough
            return VoicePack?.groupManager?.achievementList != null 
                && VoicePack.groupManager.achievementList.Count != 0;
        }

        /// <summary>
        /// Saves this.VoicePack to a binary voicepack file. Makes sure any global state that might be altered during this
        /// operation is restored.
        /// </summary>
        /// <remarks> see remarks LoadFromFile() </remarks>
        public void ExportToFile(string filename)
        {
            if (!IsValidVoicePackLoaded()) return;

            var globalBackup = new VoicePackExtended();
            globalBackup.GetFromGlobal();
            {
                this.SetAsGlobal();
                this.VoicePack.CreatePAKFile(filename);
            }
            globalBackup.SetAsGlobal();
        }

#region merge functions
        /// <summary>
        /// Merges the voicepack with filename into this (this can be empty)
        /// Returns true if it was able to load the voicepack from disk
        /// </summary>
        public bool Merge(string voicePackFilename)
        {
            VoicePackExtended other = new VoicePackExtended();
            if (other.LoadFromFile(voicePackFilename))
            {
                this.Merge(other);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Merges the achievementsOptionsComponens (aka voicepack) of other into this (this can be empty)
        /// TODO: test this properly
        /// TODO: merge ComponentInfo (Name Author Description), backgroundimage somehow, guid seems to be 00000 always
        /// </summary>
        /// <param name="other"></param>
        public void Merge(VoicePackExtended other)
        {
            //Check pre-conditions
            if(!IsValidVoicePackLoaded())
                throw new InvalidOperationException();
            if(other == null || !other.IsValidVoicePackLoaded())
                throw new ArgumentNullException();

            //Merge achievemntlist
            //Make sure both voicepacks contain all achievements, and thus have the same amount of elements, before looping through them
            VoicePack.LoadNewAchievements();
            other.VoicePack.LoadNewAchievements();

            var achievementList = VoicePack.groupManager.achievementList;
            var otherAchievementList = other.VoicePack.groupManager.achievementList;

            foreach (var acheivementPair in achievementList)
            {
                var key = acheivementPair.Key;
                AchievementOptions achievement = acheivementPair.Value;
                AchievementOptions otherAchievement = otherAchievementList[key];
                Merge(achievement, otherAchievement);
            }

            //Merge componentData
            if (other.VoicePack.componentData == null) 
                return;  //nothing to merge
            if (VoicePack.componentData == null)
            {
                VoicePack.componentData = other.VoicePack.componentData;
                return;
            }
            foreach (var otherData in other.VoicePack.componentData)
            {
                if (VoicePack.componentData.ContainsKey(otherData.Key))
                {
                    //throw new InvalidOperationException("Trying to merge with voicepack with identical key for some sound/resource.\n Key: " + otherData.Key);
                    //TODO: possible workaround: create new key and update all references (e.g. AchievementOptions.pakSoundPath, 
                    Debug.WriteLine("Duplicate key detected while merging: " + otherData.Key + Environment.NewLine);
                }
                VoicePack.componentData[otherData.Key] = otherData.Value;
            }
        }

        /// <summary>
        /// Merges otherAchievement into achievement, only used by Merge(AchievementOptionsComponents)
        /// </summary>
        private void Merge(AchievementOptions achievement, AchievementOptions otherAchievement)
        {
            if(achievement == null || otherAchievement == null)
                throw new ArgumentNullException();

            //Sounds we wish to add can be in achievement.soundFile if its one, or in BasicDynamixSoundManager.sounds if there are more.
            //First find all non-default sounds, put them in a list and then decide what to do
            var soundsToAdd = new List<BasicAchievementSound>();
            _AddNonDefaultSounds(achievement, soundsToAdd);
            _AddNonDefaultSounds(otherAchievement, soundsToAdd);

            if (soundsToAdd.Count == 1)
            {
                achievement.fileSoundPath = soundsToAdd[0].soundFile;
                achievement.pakSoundPath = soundsToAdd[0].pakSoundFile;
                achievement.dynamicSounds = null;
            }
            else if (soundsToAdd.Count > 1)
            {
                achievement.fileSoundPath = "default";
                achievement.pakSoundPath = null;
                achievement.dynamicSounds.sounds = soundsToAdd.ToArray();
            }
            //else if soundsToAdd.Count == 0, there is nothing to change, achievement has a default sound already (calling LoadNewAchievements() in Merge() makes sure of that)
        }

        /// <summary>
        /// helper function that goes through the achievement and adds all its sounds into the sounds list parameter
        /// </summary>
        private void _AddNonDefaultSounds(AchievementOptions achievement, List<BasicAchievementSound> sounds)
        {
            if (achievement.fileSoundPath.ToLower().Trim() != "default")
            {
                var soundToAdd = new BasicAchievementSound
                {
                    soundFile = achievement.fileSoundPath,
                    pakSoundFile = achievement.pakSoundPath
                };
                //apparently this is always empty
                sounds.Add(soundToAdd);
            }
            else if (achievement.dynamicSounds?.sounds != null)
            {
                sounds.AddRange(achievement.dynamicSounds.sounds);
            }
        }
        #endregion

        //public void Extract(VoicePackExtended toExtract)
        //{
        //    //use references
        //    //https://msdn.microsoft.com/en-us/library/system.object.referenceequals.aspx
        //}

#region equals functions
        /// <summary>
        /// Now: very specific equals that serves my needs, it only checks the sound filenames
        /// TODO: possibly not used anymore -> throw out
        /// </summary>
        /// <returns>true when equal, false when not equal</returns>
        public bool EqualSoundFilenames(VoicePackExtended other)
        {
            if (!this.IsValidVoicePackLoaded() && !other.IsValidVoicePackLoaded())
                return true;

            if (!this.IsValidVoicePackLoaded() || !other.IsValidVoicePackLoaded())
                return false;

            var achievements = VoicePack.groupManager.achievementList;
            var otherAchievements = other.VoicePack.groupManager.achievementList;

            //the number of achievements is presumed to be equal, when its loaded, missing achievements are added

            foreach (var achievementPair in achievements)
            {
                var key = achievementPair.Key;
                if (!otherAchievements.ContainsKey(key)) return false;
                var otherAchievement = otherAchievements[key];
                var achievement = achievementPair.Value;

                if (!VoicePackComparer.AchievementOptionsOneSoundEqual(achievement, otherAchievement))
                    return false;

                if (!VoicePackComparer.AchievementOptionsDynamicSoundsEqual(achievement, otherAchievement))
                    return false;
            }

            Trace.WriteLine("EqualSoundFilenames returned true");
            return true;
        }

        public bool EqualComponentInfo(VoicePackExtended other)
        {
            if (VoicePack?.componentInformation == null && other.VoicePack?.componentInformation == null)
                return true;
            if (VoicePack?.componentInformation == null || other.VoicePack?.componentInformation == null)
                return false;

            var compInfo = VoicePack.componentInformation;
            var otherCompInfo = other.VoicePack.componentInformation;
            return
                compInfo.name == otherCompInfo.name &&
                compInfo.author == otherCompInfo.author &&
                compInfo.description == otherCompInfo.description &&
                compInfo.sampleImage == otherCompInfo.sampleImage &&
                compInfo.backupSampleImage == otherCompInfo.backupSampleImage;
        }
        #endregion

        /// <summary>
        /// Produces a formatted string representing the contents of this instance mainly used for debugging purposes.
        /// TODO: refactor into its own class, and remove some code duplication
        /// </summary>
        /// <returns></returns>
        public string ToString()
        {
            if (VoicePack == null) return "VoicePack == null"; 

            var output = new StringBuilder();

            output.AppendLine();
            output.AppendFormat("{0,-30}{1,-30}{2}", "LoadedVoicePack: ", LoadedVoicePack, Environment.NewLine);
            output.AppendFormat("{0,-30}{1,-30}{2}", "LoadedVoicePackConfigFile: ", LoadedVoicePackConfigFile, Environment.NewLine);
            output.AppendFormat("{0,-30}{1,-30}{2}", "UsingPAK: ", UsingPAK, Environment.NewLine);

            var groupManager = VoicePack.groupManager;
            if (groupManager == null)
            {
                output.AppendLine("groupManager is null, nothing further to print");
                return output.ToString();
            }

            var componentInfo = groupManager.componentInfo;
            if (VoicePack.groupManager.componentInfo == null)
            {
                output.AppendLine("componentInfo is null");
            }
            else
            {
                output.AppendLine("ComponentInfo:");
                output.AppendFormat("{0,-30}{1,-30}{2}", " Name: ", componentInfo.name, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", " Description: ", componentInfo.description, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", " Author: ", componentInfo.author, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", " SampleImage: ", componentInfo.sampleImage, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", " backupSampleImage: ", componentInfo.backupSampleImage, Environment.NewLine);
                output.AppendLine();
            }

            output.AppendFormat("{0,-30}{1,-30}{2}", "GUID: ", groupManager.guid, Environment.NewLine);
            output.AppendFormat("{0,-30}{1,-30}{2}", "backgroundImage: ", groupManager.backgroundImage, Environment.NewLine);
            output.AppendFormat("{0,-30}{1,-30}{2}", "pakBackgroundImage: ", groupManager.pakBackgroundImage, Environment.NewLine);
            output.AppendFormat("{0,-30}{1,-30}{2}", "backgroundImageDisabled: ", groupManager.backgroundImageDisable, Environment.NewLine);
            output.AppendLine();

            var achievementList = groupManager.achievementList;
            if (achievementList == null)
            {
                output.AppendLine("achievementList is null, no achievements to print");
                return output.ToString();
            }

            output.AppendLine("Achievement List:");
            var count = 0;
            foreach (var achievement in achievementList)
            {
                if (count == 5) break;
                count++;

                if (achievement.Key == null)
                {
                    output.AppendLine("Achievement.Key is null"); 
                    continue;
                }
                if (achievement.Value == null)
                {
                    output.AppendLine("Achievement.Value is null");
                    continue;
                }
                
                output.AppendFormat("{0,-30}{1,-30}{2}", "Key: ",  achievement.Key, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", "  fileSoundPath: ", achievement.Value.fileSoundPath, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", "  pakSoundPath: ", achievement.Value.pakSoundPath, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", " dynamicSounds: ", achievement.Value.dynamicSounds, Environment.NewLine);

                if (achievement.Value.dynamicSounds == null) continue;

                var sounds = achievement.Value.dynamicSounds.sounds;
                if (sounds == null) continue;
                foreach (var sound in sounds)
                {
                    output.AppendFormat("{0,-30}{1,-30}{2}", "  soundfile: ", sound.soundFile, Environment.NewLine);
                    output.AppendFormat("{0,-30}{1,-30}{2}", "  pakSoundFile: ", sound.pakSoundFile, Environment.NewLine);
                }

                output.AppendFormat("{0,-32}{1,-30}{2}", "imageEnabledInGame: " , achievement.Value.imageEnabledInGame, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", "imageEnabledStreaming: ", achievement.Value.imageEnabledStreaming, Environment.NewLine);
                output.AppendFormat("{0,-30}{1,-30}{2}", "soundEnabled: ", achievement.Value.soundEnabled, Environment.NewLine);
            }

            output.AppendFormat("{0}ComponentData: {1} entries {0}", Environment.NewLine, VoicePack.componentData.Count);
            count = 0;
            foreach (var dataPair in VoicePack.componentData)
            {
                if (count == 5) break;
                count++;

                output.AppendFormat("{0,-30}{1,-30}{2}", "Key: ", dataPair.Key, Environment.NewLine);
            }

            return output.ToString();
        }
    }
    public class VoicePackComparer
    {
        /// <summary>
        /// Helper function that checks equality of the soundfilenames of the voicepack when it has only 1 sound
        /// </summary>
        /// <param name="lhs">left hand side</param>
        /// <param name="rhs">right hand side</param>
        /// <returns></returns>
        public static bool AchievementOptionsOneSoundEqual(AchievementOptions lhs, AchievementOptions rhs)
        {
            return lhs.fileSoundPath == rhs.fileSoundPath && lhs.pakSoundPath == rhs.pakSoundPath;
        }

        /// <summary>
        /// Helper function that checks equality of the soundfilenames fo the voicepack when it has 1+ sounds
        /// </summary>
        /// <param name="lhs">left hand side</param>
        /// <param name="rhs">right hand side</param>
        /// <returns></returns>
        public static bool AchievementOptionsDynamicSoundsEqual(AchievementOptions lhs, AchievementOptions rhs)
        {
            if (rhs.dynamicSounds?.sounds == null && lhs.dynamicSounds?.sounds == null)
                return true;
            if (rhs.dynamicSounds?.sounds == null || lhs.dynamicSounds?.sounds == null)
                return false;

            if (rhs.dynamicSounds.sounds.Length != lhs.dynamicSounds.sounds.Length)
                return false;

            foreach (var sound in rhs.dynamicSounds.sounds)
            {
                //try to find other sound according to lambda
                var otherSound = lhs.dynamicSounds.sounds.SingleOrDefault(
                    item => item.pakSoundFile == sound.pakSoundFile && item.soundFile == sound.soundFile);
                if (otherSound == null) return false;
            }

            return true;
        }

        public static bool EqualAchievementLists(AchievementList lhs, AchievementList rhs)
        {
            //var lhs = VoicePack.groupManager.achievementList;
            //var rhs = other.VoicePack.groupManager.achievementList;

            //the number of achievements is presumed to be equal, when its loaded, missing achievements are added
            //so we can loop through one list, find them in the second, and be sure the other has no extra items
            //when an equivalent is found.

            foreach (var achievementPair in lhs)
            {
                var key = achievementPair.Key;
                if (!rhs.ContainsKey(key)) return false;
                var otherAchievement = rhs[key];
                var achievement = achievementPair.Value;

                if (!VoicePackComparer.AchievementOptionsOneSoundEqual(achievement, otherAchievement))
                    return false;

                if (!VoicePackComparer.AchievementOptionsDynamicSoundsEqual(achievement, otherAchievement))
                    return false;
            }
            return true;
        }

        //public static bool 
    }

    public class VoicePackMerger
    {
        /// <summary>
        /// Merges the Achievements from otherAchievmentList into achievementList
        /// </summary>
        public static void MergeAchievementList(AchievementList AchievementList, AchievementList otherAchievementList)
        {
            foreach (var acheivementPair in AchievementList)
            {
                var key = acheivementPair.Key;
                AchievementOptions achievement = acheivementPair.Value;
                AchievementOptions otherAchievement = otherAchievementList[key];
                MergeAchievement(achievement, otherAchievement);
            }
        }

        /// <summary>
        /// Merges otherAchievement into achievement
        /// </summary>
        public static void MergeAchievement(AchievementOptions achievement, AchievementOptions otherAchievement)
        {
            if (achievement == null || otherAchievement == null)
                throw new ArgumentNullException();

            //Gather all non-default sounds from both achievements
            var soundsToAdd = new List<BasicAchievementSound>();
            AddNonDefaultSoundsToList(achievement, soundsToAdd);
            AddNonDefaultSoundsToList(otherAchievement, soundsToAdd);

            if (soundsToAdd.Count == 1)
            {
                //Create old style one sound achievement
                achievement.fileSoundPath = soundsToAdd[0].soundFile;
                achievement.pakSoundPath = soundsToAdd[0].pakSoundFile;
                achievement.dynamicSounds = null;
            }
            else if (soundsToAdd.Count > 1)
            {
                //Create dynamicSounds multi sound achievment
                achievement.fileSoundPath = "default";
                achievement.pakSoundPath = null;
                achievement.dynamicSounds.sounds = soundsToAdd.ToArray();
            }
            //else if soundsToAdd.Count == 0, there is nothing to change, achievement has a default sound already (calling LoadNewAchievements() in Merge() makes sure of that)

        }

        /// <summary>
        /// Helper function that gathers all sounds from a achievement, be it old style one sound, or multiple dynamicsounds
        /// and adds them to the List<> argument. 
        /// </summary>
        private static void AddNonDefaultSoundsToList(AchievementOptions achievement, List<BasicAchievementSound> sounds)
        {
            //Add old style one sound
            if (achievement.fileSoundPath.ToLower().Trim() != "default")
            {
                var soundToAdd = new BasicAchievementSound
                {
                    pakSoundFile = achievement.pakSoundPath,
                    soundFile = achievement.fileSoundPath
                };
                sounds.Add(soundToAdd);
            }

            //Add dynamic sounds
            if (achievement.dynamicSounds?.sounds != null)
            {
                sounds.AddRange(achievement.dynamicSounds.sounds);
            }
        }
    }


    // start of refactoring, not finished, should try TDD to get this in :D
    //internal class VoicePackMerger
    //{
    //    /// <summary>
    //    /// Merges otherAchievement into achievement, only used by Merge(AchievementOptionsComponents)
    //    /// </summary>
    //    /// <param name="achievement"></param>
    //    /// <param name="otherAchievement"></param>
    //    private static void _Merge(AchievementOptions achievement, AchievementOptions otherAchievement)
    //    {
    //        if(achievement == null || otherAchievement == null)
    //            throw new ArgumentNullException();

    //        //Sounds we wish to add can be in achievement.soundFile if its one, or in BasicDynamixSoundManager.sounds if there are more.
    //        //First find all non-default sounds, put them in a list and then decide what to do
    //        var soundsToAdd = new List<BasicAchievementSound>();
    //        _AddNonDefaultSounds(achievement, soundsToAdd);
    //        _AddNonDefaultSounds(otherAchievement, soundsToAdd);

    //        if (soundsToAdd.Count == 1)
    //        {
    //            achievement.fileSoundPath = soundsToAdd[0].soundFile;
    //            achievement.pakSoundPath = soundsToAdd[0].pakSoundFile;
    //            achievement.dynamicSounds = null;
    //        }
    //        else if (soundsToAdd.Count > 1)
    //        {
    //            achievement.fileSoundPath = "default";
    //            achievement.pakSoundPath = null;
    //            achievement.dynamicSounds.sounds = soundsToAdd.ToArray();
    //        }
    //        //else if soundsToAdd.Count == 0, there is nothing to change, achievement has a default sound already (calling LoadNewAchievements() in Merge() makes sure of that)
    //    }

    //    /// <summary>
    //    /// helper function that goes through the achievement and adds all its sounds into the sounds list parameter
    //    /// </summary>
    //    /// <param name="achievement"></param>
    //    /// <param name="sounds"></param>
    //    private static void _AddNonDefaultSounds(AchievementOptions achievement, List<BasicAchievementSound> sounds)
    //    {
    //        if (achievement.fileSoundPath.ToLower().Trim() != "default")
    //        {
    //            var soundToAdd = new BasicAchievementSound();
    //            soundToAdd.soundFile = achievement.fileSoundPath;
    //            soundToAdd.pakSoundFile = achievement.pakSoundPath; //apparently this is always empty
    //            sounds.Add(soundToAdd);
    //        }
    //        else if (achievement.dynamicSounds != null && achievement.dynamicSounds.sounds != null)
    //        {
    //            sounds.AddRange(achievement.dynamicSounds.sounds);
    //        }
    //    }


    //    /// <summary>
    //    /// Merges rhs into AchievementList and returns a reference to AchievementList
    //    /// </summary>
    //    /// <param name="AchievementList">Left hand side parameter, not null</param>
    //    /// <param name="rhs">Right hand side parameter, not null</param>
    //    /// <returns></returns>
    //    public static AchievementOptionsComponents MergeInto(AchievementOptionsComponents AchievementList,
    //        AchievementOptionsComponents rhs)
    //    {
    //        //Pre-condition
    //        if (AchievementList == null || rhs == null)
    //            throw new ArgumentNullException();

    //        //AchievementOptionsComponents merged = new AchievementOptionsComponents();

    //        //make sure all achievments are in the list: the lists/dictionaries are the same length and contain the same keys
    //        AchievementList.LoadNewAchievements();
    //        rhs.LoadNewAchievements();
    //        //merged.LoadNewAchievements();

    //        var lhsAchievements = AchievementList.groupManager.achievementList;
    //        var rhsAchievements = rhs.groupManager.achievementList;
    //        foreach (var lhsAchievementPair in lhsAchievements)
    //        {
    //            var lhsAchievement = lhsAchievementPair.Value;
    //            var rhsAchievement = rhsAchievements[lhsAchievementPair.Key];
    //            _Merge(lhsAchievement, rhsAchievement);
    //        }
    //        /*

    //        var achievementList = VoicePack.groupManager.achievementList;
    //        var otherAchievementList = other.VoicePack.groupManager.achievementList;

    //        foreach (var acheivementPair in achievementList)
    //        {
    //            var key = acheivementPair.Key;
    //            AchievementOptions achievement = acheivementPair.Value;
    //            AchievementOptions otherAchievement = otherAchievementList[key];
    //            Merge(achievement, otherAchievement);
    //        }

    //        //Merge componentData
    //        if (other.VoicePack.componentData == null) 
    //            return;  //nothing to merge
    //        if (VoicePack.componentData == null)
    //        {
    //            VoicePack.componentData = other.VoicePack.componentData;
    //            return;
    //        }
    //        foreach (var otherData in other.VoicePack.componentData)
    //        {
    //            if (VoicePack.componentData.ContainsKey(otherData.Key))
    //            {
    //                //throw new InvalidOperationException("Trying to merge with voicepack with identical key for some sound/resource.\n Key: " + otherData.Key);
    //                //TODO: possible workaround: create new key and update all references (e.g. AchievementOptions.pakSoundPath, 
    //                Debug.WriteLine("Duplicate key detected while merging: " + otherData.Key + Environment.NewLine);
    //            }
    //            VoicePack.componentData[otherData.Key] = otherData.Value;
    //        }
    //        */

    //        return null;
    //    }




    //}
}
