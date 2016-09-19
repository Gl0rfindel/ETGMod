﻿using System;
using UnityEngine;

namespace SGUI {
    public class SButton : SElement {

        public string Text;
        public Texture Icon;

        public TextAnchor Alignment = TextAnchor.MiddleLeft;

        public Vector2 Border = new Vector2(4f, 4f);

        public Action<SButton> OnClick;

        public bool StatusPrev { get; protected set; }
        public bool Status { get; protected set; }

        public override bool IsInteractive {
            get {
                return true;
            }
        }

        public bool IsClicked { get; protected set; }

        public SButton()
            : this("") { }
        public SButton(string text) {
            Text = text;
        }

        public override void UpdateStyle() {
            // This will get called again once this element gets added to the root.
            if (Root == null) return;

            if (UpdateBounds) {
                if (Parent == null) {
                    Size = Backend.MeasureText(ref Text, font: Font);
                } else {
                    Size = Backend.MeasureText(ref Text, Parent.InnerSize - Border * 2f - (Icon == null ? Vector2.zero : new Vector2(Icon.width + 1f, 0f)), font: Font);
                }
                Size += new Vector2(16f, 2f);
                Size += Border * 2f;
            }

            base.UpdateStyle();
        }

        public override void RenderBackground() { }
        public override void Render() {
            // Do not render background - background should be handled by Draw.Button

            Draw.Button(this, Vector2.zero, Size, Text, Alignment, Icon);
        }

        public override void MouseStatusChanged(EMouseStatus e, Vector2 pos) {
            base.MouseStatusChanged(e, pos);

            if (e == EMouseStatus.Down) {
                OnClick?.Invoke(this);
            }
        }

    }
}
