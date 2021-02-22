﻿using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using SGUI;
using System.IO;

public class ETGModLoaderMenu : ETGModMenu {

    public SGroup DisabledListGroup;
    public SGroup ModListGroup;

    public SGroup ModOnlineListGroup;

    public Texture2D IconMod;
    public Texture2D IconAPI;
    public Texture2D IconZip;
    public Texture2D IconDir;

    public static ETGModLoaderMenu Instance { get; protected set; }
    public ETGModLoaderMenu() {
        Instance = this;
    }

    public override void Start() {
        IconMod = Resources.Load<Texture2D>("etgmod/gui/icon_mod");
        IconAPI = Resources.Load<Texture2D>("etgmod/gui/icon_api");
        IconZip = Resources.Load<Texture2D>("etgmod/gui/icon_zip");
        IconDir = Resources.Load<Texture2D>("etgmod/gui/icon_dir");

        GUI = new SGroup {
            Visible = false,
            OnUpdateStyle = (SElement elem) => elem.Fill(),
            Children = {
                new SLabel("ETGMod <color=#ffffffff>" + ETGMod.BaseUIVersion + "</color>") {
                    Foreground = Color.gray,
                    OnUpdateStyle = elem => elem.Size.x = elem.Parent.InnerSize.x
                },

                (DisabledListGroup = new SGroup {
                    Background = new Color(0f, 0f, 0f, 0f),
                    AutoLayout = (SGroup g) => g.AutoLayoutVertical,
                    ScrollDirection = SGroup.EDirection.Vertical,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Size = new Vector2(
                            Mathf.Max(288f, elem.Parent.InnerSize.x * 0.25f),
                            Mathf.Max(256f, elem.Parent.InnerSize.y * 0.2f)
                        );
                        elem.Position = new Vector2(0f, elem.Parent.InnerSize.y - elem.Size.y);
                    },
                }),
                new SLabel("DISABLED MODS") {
                    Foreground = Color.gray,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Position = new Vector2(DisabledListGroup.Position.x, DisabledListGroup.Position.y - elem.Backend.LineHeight - 4f);
                    },
                },

                (ModListGroup = new SGroup {
                    Background = new Color(0f, 0f, 0f, 0f),
                    AutoLayout = (SGroup g) => g.AutoLayoutVertical,
                    ScrollDirection = SGroup.EDirection.Vertical,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Position = new Vector2(0f, elem.Backend.LineHeight * 2.5f);
                        elem.Size = new Vector2(
                            DisabledListGroup.Size.x,
                            DisabledListGroup.Position.y - elem.Position.y - elem.Backend.LineHeight * 1.5f
                        );
                    },
                }),
                new SLabel("ENABLED MODS") {
                    Foreground = Color.gray,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Position = new Vector2(ModListGroup.Position.x, ModListGroup.Position.y - elem.Backend.LineHeight - 4f);
                    },
                },

              /*  (ModOnlineListGroup = new SGroup {
                    Background = new Color(0f, 0f, 0f, 0f),
                    AutoLayout = (SGroup g) => g.AutoLayoutVertical,
                    ScrollDirection = SGroup.EDirection.Vertical,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Position = new Vector2(ModOnlineListGroup.Size.x + 4f, ModListGroup.Position.y);
                        elem.Size = new Vector2(
                            DisabledListGroup.Size.x,
                            elem.Parent.InnerSize.y - elem.Position.y
                        );
                    },
                }),
                new SLabel("LASTBULLET MODS") {
                    Foreground = Color.gray,
                    OnUpdateStyle = delegate (SElement elem) {
                        elem.Position = new Vector2(ModOnlineListGroup.Position.x, ModListGroup.Position.y - elem.Backend.LineHeight - 4f);
                    },
                },*/
            }
        };
    }

    public override void OnOpen() {
        RefreshMods();
        base.OnOpen();
    }

    protected Coroutine _C_RefreshMods;
    public void RefreshMods() {
        _C_RefreshMods?.StopGlobal();
        _C_RefreshMods = _RefreshMods().StartGlobal();
    }
    protected virtual IEnumerator _RefreshMods() {
        ModListGroup.Children.Clear();
        for (int i = 0; i < ETGMod.GameMods.Count; i++) {
            ETGModule mod = ETGMod.GameMods[i];
            ETGModuleMetadata meta = mod.Metadata;

            ModListGroup.Children.Add(NewEntry(meta.Name, meta.Icon));
            yield return null;
        }

        DisabledListGroup.Children.Clear();
        string[] files = Directory.GetFiles(ETGMod.ModsDirectory);
        for (int i = 0; i < files.Length; i++) {
            string file = Path.GetFileName(files[i]);
            if (!file.EndsWithInvariant(".zip")) continue;
            if (ETGMod.GameMods.Exists(mod => mod.Metadata.Archive == files[i])) continue;
            DisabledListGroup.Children.Add(NewEntry(file.Substring(0, file.Length - 4), IconZip));
            yield return null;
        }
        files = Directory.GetDirectories(ETGMod.ModsDirectory);
        for (int i = 0; i < files.Length; i++) {
            string file = Path.GetFileName(files[i]);
            if (file == "RelinkCache") continue;
            if (ETGMod.GameMods.Exists(mod => mod.Metadata.Directory == files[i])) continue;
            DisabledListGroup.Children.Add(NewEntry($"{file}/", IconDir));
            yield return null;
        }

    }

    public virtual SButton NewEntry(string name, Texture icon = null) {
        SButton button = new SButton(name) {
            Icon = icon ?? IconMod,
            With = { new SFadeInAnimation() }
        };
        return button;
    }
}
