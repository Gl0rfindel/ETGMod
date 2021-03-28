﻿#pragma warning disable RECS0018

using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using YamlDotNet.Serialization;
using AttachPoint = tk2dSpriteDefinition.AttachPoint;

public static partial class ETGMod {
    /// <summary>
    /// ETGMod asset management.
    /// </summary>
    public static partial class Assets {

        public readonly static Type t_Object = typeof(UnityEngine.Object);
        public readonly static Type t_AssetDirectory = typeof(AssetDirectory);
        public readonly static Type t_Texture = typeof(Texture);
        public readonly static Type t_Texture2D = typeof(Texture2D);
        public readonly static Type t_tk2dSpriteCollectionData = typeof(tk2dSpriteCollectionData);
        public readonly static Type t_tk2dSpriteDefinition = typeof(tk2dSpriteDefinition);

        private readonly static FieldInfo f_tk2dSpriteCollectionData_spriteNameLookupDict =
            typeof(tk2dSpriteCollectionData).GetField("spriteNameLookupDict", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Asset map. All string - AssetMetadata entries here will cause an asset to be remapped. Use ETGMod.Assets.AddMapping to add an entry.
        /// </summary>
        public readonly static Dictionary<string, AssetMetadata> Map = new Dictionary<string, AssetMetadata>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// Directories that would not fit into Map due to conflicts.
        /// </summary>
        public readonly static Dictionary<string, AssetMetadata> MapDirs = new Dictionary<string, AssetMetadata>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// Texture remappings. This dictionary starts empty and will be filled as sprites get replaced. Feel free to add your own remapping here.
        /// </summary>
        public readonly static Dictionary<string, Texture2D> TextureMap = new Dictionary<string, Texture2D>();

        private readonly static Dictionary<string, AssetSpriteCollectionLookup> _assetSpriteCollections = new Dictionary<string, AssetSpriteCollectionLookup>();

        public static bool DumpResources = false;

        public static bool DumpSprites = false;

        public static bool DumpSpritesMetadata = false;

        public static bool EnabledLegacyFileSystemTextureMapping = false;

        private readonly static Vector2[] _DefaultUVs = {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        public static Shader DefaultSpriteShader;
        public static RuntimeAtlasPacker Packer = new RuntimeAtlasPacker();
        public static Deserializer Deserializer = new DeserializerBuilder().Build();
        public static Serializer Serializer = new SerializerBuilder().Build();

        public static Vector2[] GenerateUVs(Texture2D texture, int x, int y, int width, int height) {
            return new Vector2[] {
                new Vector2((x        ) / (float) texture.width, (y         ) / (float) texture.height),
                new Vector2((x + width) / (float) texture.width, (y         ) / (float) texture.height),
                new Vector2((x        ) / (float) texture.width, (y + height) / (float) texture.height),
                new Vector2((x + width) / (float) texture.width, (y + height) / (float) texture.height),
            };
        }

        public static bool TryGetMapped(string path, out AssetMetadata metadata, bool includeDirs = false) {
            if (includeDirs) {
                if (MapDirs.TryGetValue(path, out metadata))
                    return true;
            }

            if (Map.TryGetValue(path, out metadata))
                return true;

            return false;
        }
        public static AssetMetadata GetMapped(string path) {
            AssetMetadata metadata;
            TryGetMapped(path, out metadata);
            return metadata;
        }

        public static AssetMetadata AddMapping(string path, AssetMetadata metadata) {
            path = path.Replace('\\', '/');
            if (metadata.AssetType == null) {
                path = RemoveExtension(path, out metadata.AssetType);
            }

            if (metadata.AssetType == t_AssetDirectory) {
                return MapDirs[path] = metadata;
            } else if (metadata.AssetType == t_Texture2D && path.StartsWith("sprites/")) {
                int index = path.IndexOf('/', 8);
                if (index >= 0) {
                    string collectionName = path.Substring(8, index - 8);
                    if (!_assetSpriteCollections.TryGetValue(collectionName, out var lookup)) {
                        lookup = _assetSpriteCollections[collectionName] = new AssetSpriteCollectionLookup(collectionName);
                    }

                    lookup[path] = metadata;
                }
            }

            return Map[path] = metadata;
        }

        public static string RemoveExtension(string file, out Type type) {
            type = t_Object;

            if (file.EndsWithInvariant(".png")) {
                type = t_Texture2D;
                file = file.Substring(0, file.Length - 4);
            }

            return file;
        }

        public static void Crawl(string dir, string root = null) {
            if (Path.GetDirectoryName(dir).StartsWithInvariant("DUMP"))
                return;
            if (root == null)
                root = dir;

            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                AddMapping(file.RemovePrefix(root).Substring(1), new AssetMetadata(file));
            }

            files = Directory.GetDirectories(dir);
            for (int i = 0; i < files.Length; i++) {
                string file = files[i];
                AddMapping(file.RemovePrefix(root).Substring(1), new AssetMetadata(file) {
                    AssetType = t_AssetDirectory
                });
                Crawl(file, root);
            }
        }

        public static void Crawl(Assembly asm) {
            string[] resourceNames = asm.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++) {
                string name = resourceNames[i];
                int indexOfContent = name.IndexOfInvariant("Content");
                if (indexOfContent < 0) {
                    continue;
                }
                name = name.Substring(indexOfContent + 8);
                AddMapping(name, new AssetMetadata(asm, resourceNames[i]));
            }
        }

        public static void HookUnity() {
            if (!Directory.Exists(ResourcesDirectory)) {
                Debug.Log("Resources directory not existing, creating...");
                Directory.CreateDirectory(ResourcesDirectory);
            }

            string spritesDir = Path.Combine(ResourcesDirectory, "sprites");
            if (!Directory.Exists(spritesDir)) {
                Debug.Log("Sprites directory not existing, creating...");
                Directory.CreateDirectory(spritesDir);
            }

            ETGModUnityEngineHooks.Load = Load;
            // ETGModUnityEngineHooks.LoadAsync = LoadAsync;
            // ETGModUnityEngineHooks.LoadAll = LoadAll;
            // ETGModUnityEngineHooks.UnloadAsset = UnloadAsset;

            DefaultSpriteShader = Shader.Find("tk2d/BlendVertexColor");
        }

        public static UnityEngine.Object Load(string path, Type type) {
            if (path == "PlayerCoopCultist" && Player.CoopReplacement != null) {
                path = Player.CoopReplacement;
            } else if (path.StartsWithInvariant("Player") && Player.PlayerReplacement != null) {
                path = Player.PlayerReplacement;
            }

#if DEBUG
            if (DumpResources) {
                Dump.DumpResource(path);
            }
#endif

            AssetMetadata metadata;
            bool isJson = false;
            bool isPatch = false;
            if (!TryGetMapped(path, out metadata, true)) {
                if (TryGetMapped(path + ".json", out metadata)) {
                    isJson = true;
                } else if (TryGetMapped(path + ".patch.json", out metadata)) {
                    isPatch = true;
                    isJson = true;
                }
            }

            if (metadata != null) {
                if (isJson) {
                    if (isPatch) {
                        UnityEngine.Object obj = Resources.Load(path + ETGModUnityEngineHooks.SkipSuffix);
                        using (JsonHelperReader json = JSONHelper.OpenReadJSON(metadata.Stream)) {
                            json.Read(); // Go to start;
                            return (UnityEngine.Object) json.FillObject(obj);
                        }
                    }
                    return (UnityEngine.Object) JSONHelper.ReadJSON(metadata.Stream);
                }

                if (t_tk2dSpriteCollectionData == type) {
                    AssetMetadata json = GetMapped(path + ".json");
                    if (metadata.AssetType == t_Texture2D && json != null) {
                        // Atlas
                        string[] names;
                        Rect[] regions;
                        Vector2[] anchors;
                        AttachPoint[][] attachPoints;
                        AssetSpriteData.ToTK2D(JSONHelper.ReadJSON<List<AssetSpriteData>>(json.Stream), out names, out regions, out anchors, out attachPoints);
                        tk2dSpriteCollectionData sprites = tk2dSpriteCollectionData.CreateFromTexture(
                            Resources.Load<Texture2D>(path), tk2dSpriteCollectionSize.Default(), names, regions, anchors
                        );
                        for (int i = 0; i < attachPoints.Length; i++) {
                            sprites.SetAttachPoints(i, attachPoints[i]);
                        }
                        return sprites;
                    }

                    if (metadata.AssetType == t_AssetDirectory) {
                        // Separate textures
                        // TODO create collection from "children" assets
                        tk2dSpriteCollectionData data = new GameObject(path.StartsWithInvariant("sprites/") ? path.Substring(8) : path).AddComponent<tk2dSpriteCollectionData>();
                        tk2dSpriteCollectionSize size = tk2dSpriteCollectionSize.Default();

                        data.spriteCollectionName = data.name;
                        data.Transient = true;
                        data.version = 3;
                        data.invOrthoSize = 1f / size.OrthoSize;
                        data.halfTargetHeight = size.TargetHeight * 0.5f;
                        data.premultipliedAlpha = false;
                        data.material = new Material(DefaultSpriteShader);
                        data.materials = new Material[] { data.material };
                        data.buildKey = UnityEngine.Random.Range(0, int.MaxValue);

                        data.Handle();

                        data.textures = new Texture2D[data.spriteDefinitions.Length];
                        for (int i = 0; i < data.spriteDefinitions.Length; i++) {
                            data.textures[i] = data.spriteDefinitions[i].materialInst.mainTexture;
                        }

                        return data;
                    }
                }

                if (t_Texture.IsAssignableFrom(type) ||
                    type == t_Texture2D ||
                    (type == t_Object && metadata.AssetType == t_Texture2D)) {
                    var tex = new Texture2D(2, 2);
                    tex.name = path;
                    tex.LoadImage(metadata.Data);
                    tex.filterMode = FilterMode.Point;
                    return tex;
                }

            }

            UnityEngine.Object orig = Resources.Load(path + ETGModUnityEngineHooks.SkipSuffix, type);
            if (orig is GameObject o) {
                Objects.HandleGameObject(o);
            }
            return orig;
        }

        public static void HandleSprites(tk2dSpriteCollectionData sprites) {
            if (!sprites) {
                return;
            }

            string path = "sprites/" + sprites.spriteCollectionName;
            ProcessSpritePath(sprites, path);

            if (DumpSprites) {
                Dump.DumpSpriteCollection(sprites);
            }
            if (DumpSpritesMetadata) {
                Dump.DumpSpriteCollectionMetadata(sprites);
            }

            if (!_assetSpriteCollections.TryGetValue(sprites.spriteCollectionName, out var lookup))
                return;

            if (!lookup.TakeUnprocessedChanges(out var unprocessedChanges))
                return;

            List<tk2dSpriteDefinition> list = null;
            foreach (KeyValuePair<string, AssetMetadata> mapping in unprocessedChanges) {
                string assetPath = mapping.Key;
                try {
                    string name = assetPath.Substring(path.Length + 1);
                    ProcessFrame(sprites, name, assetPath, ref list);
                } catch (Exception e) {
                    Debug.Log($"Exception while processing {assetPath} for sprite collection {sprites.spriteCollectionName}");
                    Debug.LogException(e);
                }
            }

            if (list != null) {
                sprites.spriteDefinitions = list.ToArray();
                f_tk2dSpriteCollectionData_spriteNameLookupDict.SetValue(sprites, null);
            }

            if (sprites.hasPlatformData) {
                sprites.inst.Handle();
            }
        }

        public static void HandleDfAtlas(dfAtlas atlas) {
            if (atlas == null) {
                return;
            }
            string path = "sprites/DFGUI/" + atlas.name;

            Texture2D replacement;
            AssetMetadata metadata;

            Texture mainTexture = atlas.Material.mainTexture;
            string atlasName = mainTexture?.name;
            if (mainTexture != null && (atlasName == null || atlasName.Length == 0 || atlasName[0] != '~')) {
                if (TextureMap.TryGetValue(path, out replacement)) { } else if (TryGetMapped(path, out metadata)) { TextureMap[path] = replacement = Resources.Load<Texture2D>(path); } else {

                    if (EnabledLegacyFileSystemTextureMapping) {
                        foreach (KeyValuePair<string, AssetMetadata> mapping in Map) {
                            if (!mapping.Value.HasData) continue;
                            string resourcePath = mapping.Key;
                            if (!resourcePath.StartsWithInvariant("sprites/DFGUI/@")) continue;
                            string spriteName = resourcePath.Substring(9);
                            if (atlas.name.Contains(spriteName)) {
                                string copyPath = Path.Combine(ResourcesDirectory, ("DUMP" + path).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) + ".png");
                                if (mapping.Value.Container == AssetMetadata.ContainerType.Filesystem && !File.Exists(copyPath)) {
                                    Directory.GetParent(copyPath).Create();
                                    File.Copy(mapping.Value.File, copyPath);
                                }
                                TextureMap[path] = replacement = Resources.Load<Texture2D>(resourcePath);
                                break;
                            }
                        }
                    }
                }

                if (replacement != null) {
                    // Full atlas texture replacement.
                    replacement.name = '~' + atlasName;
                    atlas.Material.mainTexture = replacement;
                }
            }

            /*
            if (DumpSprites) {
                Dump.DumpSpriteCollection(sprites);
            }
            if (DumpSpritesMetadata) {
                Dump.DumpSpriteCollectionMetadata(sprites);
            }
            */

            // TODO items in dfAtlas!

            if (atlas.Replacement != null) HandleDfAtlas(atlas.Replacement);

        }

        public static void ReplaceTexture(tk2dSpriteDefinition frame, Texture2D replacement, bool pack = true) {
            frame.flipped = tk2dSpriteDefinition.FlipMode.None;
            frame.materialInst = new Material(frame.material);
            frame.texelSize = replacement.texelSize;
            frame.extractRegion = pack;
            if (pack) {
                RuntimeAtlasSegment segment = Packer.Pack(replacement);
                frame.materialInst.mainTexture = segment.texture;
                frame.uvs = segment.uvs;
            } else {
                frame.materialInst.mainTexture = replacement;
                frame.uvs = _DefaultUVs;
            }
        }

        private static void ProcessSpritePath(tk2dSpriteCollectionData sprites, string path) {
            if (sprites.materials == null || sprites.materials.Length == 0)
                return;

            var material = sprites.materials[0];
            if (!material)
                return;

            Texture mainTexture = material.mainTexture;
            if (!mainTexture)
                return;

            string atlasName = mainTexture.name;
            if (string.IsNullOrEmpty(atlasName))
                return;

            if (atlasName[0] == '~')
                return;

            if (!TextureMap.TryGetValue(path, out var replacement)) {
                if (TryGetMapped(path, out _)) {
                    TextureMap[path] = replacement = Resources.Load<Texture2D>(path);
                } else {
                    if (EnabledLegacyFileSystemTextureMapping) {
                        foreach (KeyValuePair<string, AssetMetadata> mapping in Map) {
                            if (!mapping.Value.HasData)
                                continue;

                            string resourcePath = mapping.Key;
                            if (!resourcePath.StartsWithInvariant("sprites/@"))
                                continue;

                            string spriteName = resourcePath.Substring(9);
                            if (sprites.spriteCollectionName.Contains(spriteName)) {
                                string copyPath = Path.Combine(ResourcesDirectory, ("DUMP" + path).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) + ".png");
                                if (mapping.Value.Container == AssetMetadata.ContainerType.Filesystem && !File.Exists(copyPath)) {
                                    Directory.GetParent(copyPath).Create();
                                    File.Copy(mapping.Value.File, copyPath);
                                }
                                TextureMap[path] = replacement = Resources.Load<Texture2D>(resourcePath);
                                break;
                            }
                        }
                    }
                }
            }

            if (replacement) {
                // Full atlas texture replacement.
                replacement.name = '~' + atlasName;
                for (int i = 0; i < sprites.materials.Length; i++) {
                    if (sprites.materials[i]?.mainTexture == null)
                        continue;

                    sprites.materials[i].mainTexture = replacement;
                }
            }
        }

        private static void ProcessFrame(tk2dSpriteCollectionData sprites, string name, string assetPath, ref List<tk2dSpriteDefinition> newSprites) {
            tk2dSpriteDefinition frame = sprites.GetSpriteDefinition(name);

            if (frame != null && frame.materialInst != null) {
                if (Packer.IsPageTexture(frame.materialInst.mainTexture))
                    return;
            }

            if (!TextureMap.TryGetValue(assetPath, out var replacement))
                replacement = TextureMap[assetPath] = Resources.Load<Texture2D>(assetPath);

            if (replacement == null)
                return;

            if (frame == null && name[0] == '@') {
                name = name.Substring(1);
                for (int i = 0; i < sprites.spriteDefinitions.Length; i++) {
                    tk2dSpriteDefinition frame_ = sprites.spriteDefinitions[i];
                    if (frame_.Valid && frame_.name.Contains(name)) {
                        frame = frame_;
                        name = frame_.name;
                        break;
                    }
                }

                if (frame == null)
                    return;
            }

            if (frame != null) {
                // Replace old sprite.
                frame.ReplaceTexture(replacement);
            } else {
                // Add new sprite.
                if (newSprites == null) {
                    newSprites = new List<tk2dSpriteDefinition>(sprites.spriteDefinitions?.Length ?? 32);
                    if (sprites.spriteDefinitions != null) {
                        newSprites.AddRange(sprites.spriteDefinitions);
                    }
                }

                frame = new tk2dSpriteDefinition();
                frame.name = name;
                frame.material = sprites.materials[0];
                frame.ReplaceTexture(replacement);

                AssetSpriteData frameData = default;
                AssetMetadata jsonMetadata = GetMapped(assetPath + ".json");
                if (jsonMetadata != null) {
                    frameData = JSONHelper.ReadJSON<AssetSpriteData>(jsonMetadata.Stream);
                }

                frame.normals = Array<Vector3>.Empty;
                frame.tangents = Array<Vector4>.Empty;
                frame.indices = new int[] { 0, 3, 1, 2, 3, 0 };

                // TODO figure out this black magic
                const float pixelScale = 0.0625f;
                float w = replacement.width * pixelScale;
                float h = replacement.height * pixelScale;
                frame.position0 = new Vector3(0f, 0f, 0f);
                frame.position1 = new Vector3(w, 0f, 0f);
                frame.position2 = new Vector3(0f, h, 0f);
                frame.position3 = new Vector3(w, h, 0f);
                frame.boundsDataCenter = frame.untrimmedBoundsDataCenter = new Vector3(w / 2f, h / 2f, 0f);
                frame.boundsDataExtents = frame.untrimmedBoundsDataExtents = new Vector3(w, h, 0f);

                sprites.SetAttachPoints(newSprites.Count, frameData.attachPoints);

                newSprites.Add(frame);
            }
        }
    }

    public static void Handle(this tk2dBaseSprite sprite) {
        Assets.HandleSprites(sprite.Collection);
    }
    public static void HandleAuto(this tk2dBaseSprite sprite) {
        _HandleAuto(sprite.Handle);
    }

    public static void Handle(this tk2dSpriteCollectionData sprites) {
        Assets.HandleSprites(sprites);
    }

    public static void Handle(this dfAtlas atlas) {
        Assets.HandleDfAtlas(atlas);
    }
    public static void HandleAuto(this dfAtlas atlas) {
        _HandleAuto(atlas.Handle);
    }

    public static void MapAssets(this Assembly asm) {
        Assets.Crawl(asm);
    }

    public static void ReplaceTexture(this tk2dSpriteDefinition frame, Texture2D replacement, bool pack = true) {
        Assets.ReplaceTexture(frame, replacement, pack);
    }
}
