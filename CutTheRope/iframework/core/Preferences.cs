using CutTheRope.ios;
using System;
using System.Collections.Generic;
using System.IO;

namespace CutTheRope.iframework.core
{
    internal class Preferences : NSObject
    {
        private static readonly Dictionary<string, int> IntData = [];
        private static readonly Dictionary<string, string> StringData = [];
        private static bool _gameSaveRequested;
        private const string SaveFileName = "ctr_save.bin";

        public static bool GameSaveRequested
        {
            get => _gameSaveRequested;
            set => _gameSaveRequested = value;
        }

        public override NSObject Init()
        {
            if (base.Init() == null)
                return null;

            LoadPreferences();
            return this;
        }

        /// <summary>
        /// Sets an integer preference and optionally saves to disk.
        /// </summary>
        public static void SetIntForKey(int value, string key, bool commit = false)
        {
            IntData[key] = value;
            if (commit)
                RequestSave();
        }

        /// <summary>
        /// Sets a boolean preference and optionally saves to disk.
        /// </summary>
        public static void SetBooleanForKey(bool value, string key, bool commit = false)
        {
            SetIntForKey(value ? 1 : 0, key, commit);
        }

        /// <summary>
        /// Sets a string preference and optionally saves to disk.
        /// </summary>
        public static void SetStringForKey(string value, string key, bool commit = false)
        {
            StringData[key] = value;
            if (commit)
                RequestSave();
        }

        /// <summary>
        /// Gets an integer preference. Returns 0 if not found.
        /// </summary>
        public static int GetIntForKey(string key)
        {
            return IntData.TryGetValue(key, out int value) ? value : 0;
        }

        /// <summary>
        /// Gets a boolean preference. Returns false if not found.
        /// </summary>
        public static bool GetBooleanForKey(string key)
        {
            return GetIntForKey(key) != 0;
        }

        /// <summary>
        /// Gets a string preference. Returns empty string if not found.
        /// </summary>
        public static string GetStringForKey(string key)
        {
            return StringData.TryGetValue(key, out string value) ? value : "";
        }

        /// <summary>
        /// Requests the preferences to be saved on the next Update call.
        /// </summary>
        public static void RequestSave()
        {
            if (!GameSaveRequested)
                GameSaveRequested = true;
        }

        /// <summary>
        /// Saves pending preferences to disk if requested.
        /// Called once per frame by the game loop.
        /// </summary>
        public static void Update()
        {
            if (!GameSaveRequested)
                return;

            try
            {
                using (FileStream fileStream = File.Create(SaveFileName))
                {
                    SaveToStream(fileStream);
                }
                GameSaveRequested = false;
            }
            catch (Exception ex)
            {
                LOG($"Error saving preferences: {ex}");
                GameSaveRequested = false;
            }
        }

        /// <summary>
        /// Serializes all preferences to a binary stream.
        /// </summary>
        public static bool SaveToStream(Stream stream)
        {
            try
            {
                using BinaryWriter writer = new(stream);
                // Save integers
                writer.Write(IntData.Count);
                foreach (var kvp in IntData)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }

                // Save strings
                writer.Write(StringData.Count);
                foreach (var kvp in StringData)
                {
                    writer.Write(kvp.Key);
                    writer.Write(kvp.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                LOG($"Error: cannot save, {ex}");
                return false;
            }
        }

        /// <summary>
        /// Deserializes all preferences from a binary stream.
        /// </summary>
        public static bool LoadFromStream(Stream stream)
        {
            try
            {
                using BinaryReader reader = new(stream);
                // Load integers
                int intCount = reader.ReadInt32();
                for (int i = 0; i < intCount; i++)
                {
                    string key = reader.ReadString();
                    int value = reader.ReadInt32();
                    IntData.Add(key, value);
                }

                // Load strings
                int stringCount = reader.ReadInt32();
                for (int i = 0; i < stringCount; i++)
                {
                    string key = reader.ReadString();
                    string value = reader.ReadString();
                    StringData.Add(key, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                LOG($"Error: cannot load, {ex}");
                return false;
            }
        }

        /// <summary>
        /// Loads preferences from disk if the save file exists.
        /// </summary>
        public static void LoadPreferences()
        {
            if (File.Exists(SaveFileName))
            {
                try
                {
                    using FileStream fileStream = File.OpenRead(SaveFileName);
                    LoadFromStream(fileStream);
                }
                catch (Exception ex)
                {
                    LOG($"Error loading preferences: {ex}");
                }
            }
        }

        /// <summary>
        /// Instance method for compatibility. Loads preferences.
        /// </summary>
        public virtual void loadPreferences()
        {
            LoadPreferences();
        }

        /// <summary>
        /// Instance method for compatibility. Requests save.
        /// </summary>
        public virtual void SavePreferences()
        {
            RequestSave();
        }

    }
}
