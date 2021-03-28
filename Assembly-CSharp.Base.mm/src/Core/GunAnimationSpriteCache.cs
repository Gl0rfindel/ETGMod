﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Used to cache animation frames so that we do not need to recan sprite collections on every update to gun animations.
/// </summary>
internal class GunAnimationSpriteCache {
    private static readonly string[] WellKnownAnimationNames = new[] {
        "idle",
        "intro",
        "empty",
        "fire",
        "reload",
        "charge",
        "out_of_ammo",
        "discharge",
        "final_fire",
        "empty_reload",
        "critical_fire",
        "enemy_pre_fire",
        "alternate_shoot",
        "alternate_reload",
    };

    private static readonly Regex ExtractingRegex = CreateRegularExpression();

    /// <summary>
    /// Organized by sprite collection name.
    /// </summary>
    private Dictionary<string, GunAnimationSpriteGroup> _gameSpriteCollections;

    public GunAnimationSpriteCache() {
        _gameSpriteCollections = new Dictionary<string, GunAnimationSpriteGroup>();
    }

    public bool UpdateCollection(tk2dSpriteCollectionData collection) {
        if (!collection)
            return false;

        bool update;
        if (_gameSpriteCollections.TryGetValue(collection.name, out var spriteGroup)) {
            update = spriteGroup.IdentityObject != collection.spriteDefinitions;
        } else {
            spriteGroup = new GunAnimationSpriteGroup(collection.name, collection.spriteDefinitions);
            _gameSpriteCollections[collection.name] = spriteGroup;
            update = true;
        }

        if (!update)
            return false;

        spriteGroup.IdentityObject = collection.spriteDefinitions;
        for (int i = 0; i < collection.spriteDefinitions.Length; i++) {
            var sprite = collection.spriteDefinitions[i];
            if (!sprite.Valid)
                continue;

            var match = ExtractingRegex.Match(sprite.name);
            if (!match.Success)
                continue;

            string gunName = match.Groups["gun"].Value;
            string animationName = match.Groups["anim"].Value;
            if (!int.TryParse(match.Groups["order"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int order))
                continue;

            spriteGroup.SetFrame(gunName, animationName, order, collection, i);
        }

        return true;
    }

    public tk2dSpriteAnimationFrame[] TryGetAnimationFrames(string collectionName, string gunName, string animation) {
        if (!_gameSpriteCollections.TryGetValue(collectionName, out var groupCollection)) {
            return null;
        }

        if (!groupCollection.TryGetAnimationFrames(gunName, animation, out var frames)) {
            return null;
        }

        return frames;
    }

    private static Regex CreateRegularExpression() {
        string animationNames = string.Join("|", WellKnownAnimationNames);
        return new Regex($"^(?<gun>.*?)_(?<anim>{animationNames})_(?<order>\\d+)$");
    }

    private class GunAnimationSpriteGroup {
        private Dictionary<GunAndAnimationKey, FrameCollection> _framesByAnimation;

        public GunAnimationSpriteGroup(string name, object identityObject) {
            Name = name;
            IdentityObject = identityObject;
            _framesByAnimation = new Dictionary<GunAndAnimationKey, FrameCollection>();
        }

        public string Name { get; }

        /// <summary>
        /// If this associated object changes we will rescan and update.
        /// </summary>
        public object IdentityObject { get; set; }

        public void SetFrame(string gunName, string animationName, int order, tk2dSpriteCollectionData collection, int index) {
            var key = new GunAndAnimationKey(gunName, animationName);
            if (!_framesByAnimation.TryGetValue(key, out var frames)) {
                frames = new FrameCollection();
                _framesByAnimation[key] = frames;
            }

            frames.SetFrame(order, new tk2dSpriteAnimationFrame() {
                spriteCollection = collection,
                spriteId = index
            });
        }

        public bool TryGetAnimationFrames(string gunName, string animationName, out tk2dSpriteAnimationFrame[] frames) {
            var key = new GunAndAnimationKey(gunName, animationName);
            if (_framesByAnimation.TryGetValue(key, out var frameCollection)) {
                frames = frameCollection.GetFrames();
                return true;
            }

            frames = null;
            return false;
        }

        internal readonly struct GunAndAnimationKey : IEquatable<GunAndAnimationKey> {
            public GunAndAnimationKey(string gun, string animation) {
                GunName = gun;
                Animation = animation;
            }

            public string GunName { get; }

            public string Animation { get; }

            public override int GetHashCode() {
                int hc = ((GunName?.GetHashCode() ?? 0) * 17) << 16;
                hc ^= Animation?.GetHashCode() ?? 0;
                return hc;
            }

            public override bool Equals(object obj) {
                if (obj is GunAndAnimationKey other) {
                    return Equals(this, other);
                }

                return false;
            }

            public bool Equals(GunAndAnimationKey other) => Equals(this, other);

            private static bool Equals(in GunAndAnimationKey left, in GunAndAnimationKey right) {
                return left.GunName == right.GunName && left.Animation == right.Animation;
            }
        }

        internal class FrameCollection {
            private SortedList<int, tk2dSpriteAnimationFrame> _orderedFrames;
            private tk2dSpriteAnimationFrame[] _frames;
            private bool _dirty;

            public FrameCollection() {
                _orderedFrames = new SortedList<int, tk2dSpriteAnimationFrame>();
                _frames = null;
                _dirty = false;
            }

            public tk2dSpriteAnimationFrame[] GetFrames() {
                if (_dirty) {
                    if (_orderedFrames.Count == 0) {
                        _frames = null;
                    } else {
                        _frames = _orderedFrames.Values.ToArray();
                    }

                    _dirty = false;
                }

                return _frames;
            }

            public void SetFrame(int order, tk2dSpriteAnimationFrame frame) {
                _orderedFrames[order] = frame;
                _frames = null;
                _dirty = true;
            }

            public void Clear() {
                _orderedFrames.Clear();
                _frames = null;
                _dirty = false;
            }
        }
    }
}
