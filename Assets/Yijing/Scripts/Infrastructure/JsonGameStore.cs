using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Yijing.Domain.Configuration;
using Yijing.Domain.Gameplay;

namespace Yijing.Infrastructure
{
    public sealed class JsonGameStore : IGameStore
    {
        [Serializable] private sealed class Envelope { public string payload, sha256; }
        private readonly string path;
        private readonly PrototypeConfig config;
        private bool primaryValid;
        public string RecoveryNotice { get; private set; }
        public JsonGameStore(string path, PrototypeConfig config) { this.path = path; this.config = config; }

        public GameState Load()
        {
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            Exception failure = null;
            if (File.Exists(path)) {
                try { var state = Read(path); primaryValid = true; return state; }
                catch (Exception e) when (e is IOException || e is ArgumentException) { failure = e; }
            }
            if (File.Exists(path + ".bak")) {
                try { var state = Read(path + ".bak"); primaryValid = false;
                    RecoveryNotice = "已恢复上一份有效存档，原文件已保留。"; return state; }
                catch (Exception e) when (e is IOException || e is ArgumentException) { failure = e; }
            }
            throw new InvalidDataException("存档无法读取。原文件已保留，请勿删除；恢复后再继续。", failure);
        }

        public void Save(GameState state)
        {
            state.Validate(config);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string payload = JsonUtility.ToJson(state);
            var data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = payload, sha256 = Hash(payload) }));
            string temporary = path + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
                file.Write(data, 0, data.Length); file.Flush(true);
            }
            Read(temporary);
            if (File.Exists(path) && primaryValid) File.Replace(temporary, path, path + ".bak");
            else {
                if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                File.Move(temporary, path);
            }
            primaryValid = true;
        }

        private GameState Read(string file)
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
            if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.sha256 != Hash(envelope.payload))
                throw new InvalidDataException("Save checksum mismatch.");
            var state = JsonUtility.FromJson<GameState>(envelope.payload);
            if (state == null) throw new InvalidDataException("Missing save state.");
            state.UpgradeLegacy();
            state.Validate(config); return state;
        }

        private static string Hash(string value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
