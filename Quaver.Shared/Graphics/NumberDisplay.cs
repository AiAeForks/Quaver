/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 * Copyright (c) Swan & The Quaver Team <support@quavergame.com>.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Quaver.Shared.Config;
using Quaver.Shared.Helpers;
using Quaver.Shared.Skinning;
using Wobble;
using Wobble.Graphics;
using Wobble.Graphics.Sprites;

namespace Quaver.Shared.Graphics
{
    public class NumberDisplay : Sprite
    {
        /// <summary>
        ///     The value of the number display
        /// </summary>
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                // Here we run a check to see if the value incoming isn't the same as the
                // already set one. If is then we skip over it. If not, we set the new value,
                // and re-initialize the digits. This is so that we aren't looping more times than
                // we should per frame.
                if (_value == value)
                    return;

                _value = value;
                LastValueChangeTime = GameBase.Game.TimeRunning;

                // Only initialize if Digits has already been created.
                if (Digits != null)
                    InitializeDigits();
            }
        }

        /// <summary>
        /// </summary>
        public double TargetValue { get; private set; }

        /// <summary>
        ///     The value that is currently displayed
        /// </summary>
        public double CurrentValue { get; private set; }

        /// <summary>
        ///     The type of number display this is.
        /// </summary>
        private NumberDisplayType Type { get; }

        /// <summary>
        ///     Regular expression for all the allowed characters.
        /// </summary>
        private static Regex AllowedCharacters { get; } = new Regex(@"(\d|%|\.|:|-)+");

        /// <summary>
        ///     The digits in the number display.
        /// </summary>
        internal List<Sprite> Digits { get; }

        /// <summary>
        ///     The last time the value was changed
        ///     (Used for timing animations for example).
        /// </summary>
        private long LastValueChangeTime { get; set; }

        /// <summary>
        ///     The size of the digits.
        /// </summary>
        private Vector2 ImageScale { get; }

        /// <inheritdoc />
        /// <summary>
        ///     Ctor -
        /// </summary>
        /// <param name="type"></param>
        /// <param name="startingValue"></param>
        /// <param name="imageScale"></param>
        /// <param name="position"></param>
        internal NumberDisplay(NumberDisplayType type, string startingValue, Vector2 imageScale)
        {
            ImageScale = imageScale;
            Value = startingValue;
            CurrentValue = 0;
            Type = type;
            Alpha = 0;

            // First validate the initial value to see if everything is correct.
            Validate();

            // Create and initialize the digits.
            Digits = new List<Sprite>();
            InitializeDigits();
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Update(GameTime gameTime)
        {
            const float animTime = 100f;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (CurrentValue != TargetValue)
            {
                switch (Type)
                {
                    case NumberDisplayType.Score:
                        CurrentValue = MathHelper.Lerp((float)CurrentValue, (float)TargetValue,
                            (float)Math.Min(gameTime.ElapsedGameTime.TotalMilliseconds / animTime, 1));
                        break;
                    case NumberDisplayType.Combo:
                    case NumberDisplayType.Rating:
                        CurrentValue = TargetValue;
                        break;
                    case NumberDisplayType.Accuracy:
                        CurrentValue = MathHelper.Lerp((float)CurrentValue, (float)TargetValue,
                            (float)Math.Min(gameTime.ElapsedGameTime.TotalMilliseconds / animTime, 1));
                        break;
                    case NumberDisplayType.SongTime:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                switch (Type)
                {
                    case NumberDisplayType.Score:
                        Value = StringHelper.ScoreToString((int)Math.Ceiling(CurrentValue));
                        break;
                    case NumberDisplayType.Combo:
                        Value = ((int)Math.Ceiling(CurrentValue)).ToString();
                        break;
                    case NumberDisplayType.Accuracy:
                        Value = StringHelper.AccuracyToString((float)CurrentValue);
                        break;
                    case NumberDisplayType.Rating:
                        Value = StringHelper.RatingToString(CurrentValue);
                        break;
                    case NumberDisplayType.SongTime:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            base.Update(gameTime);
        }

        /// <summary>
        ///     Makes the display visible.
        /// </summary>
        internal void MakeVisible()
        {
            if (Visible)
                return;

            Visible = true;

            // Only make the digits we're using visible.
            for (var i = 0; i < Value.Length; i++)
                Digits[i].Visible = true;
        }

        /// <summary>
        ///     Makes the display invisible.
        /// </summary>
        internal void MakeInvisible()
        {
            if (!Visible)
                return;

            Visible = false;
            Digits.ForEach(x => x.Visible = false);
        }

        /// <summary>
        ///     Validates the current value to see if it is a correct number.
        ///     If it isn't, it'll throw an exception.
        /// </summary>
        private void Validate()
        {
            foreach (var c in Value)
            {
                if (!AllowedCharacters.IsMatch(c.ToString()))
                    throw new ArgumentException($"{c} is not a valid value for NumberDisplay.");
            }
        }

        /// <summary>
        ///   Goes through each character in the value and either initializes the sprite
        ///   or updates the texture of it.
        /// </summary>
        private void InitializeDigits()
        {
            var recomputeWidth = false;
            var recomputeHeight = false;

            // Go through each character and either initialize/update the sprite with the correct
            // texture.
            for (var i = 0; i < Value.Length; i++)
            {
                // If the digit doesn't already exist, we need to create it.
                if (i >= Digits.Count)
                {
                    Digits.Add(new Sprite
                    {
                        Parent = this,
                        Image = CharacterToTexture(Value[i]),
                    });

                    // Set size
                    Digits[i].Size = new ScalableVector2(Digits[i].Image.Width * ImageScale.X, Digits[i].Image.Height * ImageScale.Y);
                    recomputeWidth = true;
                    recomputeHeight = true;
                }
                // If the digit already exists, then we need to just update the texture of it.
                else
                {
                    var previousSize = Digits[i].Size;

                    Digits[i].Image = CharacterToTexture(Value[i]);
                    Digits[i].Size = new ScalableVector2(Digits[i].Image.Width * ImageScale.X, Digits[i].Image.Height * ImageScale.Y);

                    if (Digits[i].Size.X.Value != previousSize.X.Value)
                        recomputeWidth = true;

                    if (Digits[i].Size.Y.Value != previousSize.Y.Value)
                        recomputeHeight = true;
                }

                // Reset the sprite to be visible.
                if (!Digits[i].Visible)
                {
                    Digits[i].Visible = true;
                    recomputeWidth = true;
                    recomputeHeight = true;
                }
            }

            // Now check if the length of the digits matches the one of the value,
            // if it doesn't then we need to handle some of the extra lost digits.
            if (Value.Length != Digits.Count)
            {
                for (var i = Value.Length; i < Digits.Count; i++)
                {
                    if (Digits[i].Visible)
                    {
                        Digits[i].Visible = false;
                        recomputeWidth = true;
                        recomputeHeight = true;
                    }
                }
            }

            if (recomputeWidth)
            {
                var totalWidth = 0f;

                for (var i = 0; i < Digits.Count; i++)
                {
                    if (!Digits[i].Visible)
                        break;

                    totalWidth += Digits[i].Width;

                    // If it's the first image, set the x pos to 0.
                    if (i == 0)
                        Digits[i].X = 0;
                    // Otherwise, make it next to the previous one.
                    else
                        Digits[i].X = Digits[i - 1].X + Digits[i - 1].Width;
                }

                // Update the width.
                Width = totalWidth;
            }

            if (recomputeHeight)
                Height = Digits.Where(x => x.Visible).Max(x => x.Height);
        }

        /// <summary>
        ///     Updates the value of the number idsplay
        /// </summary>
        /// <param name="num"></param>
        public void UpdateValue(double num) => TargetValue = num;

        /// <summary>
        ///     Converts a single character into a texture.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        private Texture2D CharacterToTexture(char c)
        {
            switch (c)
            {
                // 0
                case '0' when Type == NumberDisplayType.Score:
                case '0' when Type == NumberDisplayType.Accuracy:
                case '0' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[0];
                case '0' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[0];
                case '0' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[0];
                // 1
                case '1' when Type == NumberDisplayType.Score:
                case '1' when Type == NumberDisplayType.Accuracy:
                case '1' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[1];
                case '1' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[1];
                case '1' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[1];
                // 2
                case '2' when Type == NumberDisplayType.Score:
                case '2' when Type == NumberDisplayType.Accuracy:
                case '2' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[2];
                case '2' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[2];
                case '2' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[2];
                // 3
                case '3' when Type == NumberDisplayType.Score:
                case '3' when Type == NumberDisplayType.Accuracy:
                case '3' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[3];
                case '3' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[3];
                case '3' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[3];
                // 4
                case '4' when Type == NumberDisplayType.Score:
                case '4' when Type == NumberDisplayType.Accuracy:
                case '4' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[4];
                case '4' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[4];
                case '4' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[4];
                // 5
                case '5' when Type == NumberDisplayType.Score:
                case '5' when Type == NumberDisplayType.Accuracy:
                case '5' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[5];
                case '5' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[5];
                case '5' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[5];
                // 6
                case '6' when Type == NumberDisplayType.Score:
                case '6' when Type == NumberDisplayType.Accuracy:
                case '6' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[6];
                case '6' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[6];
                case '6' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[6];
                // 7
                case '7' when Type == NumberDisplayType.Score:
                case '7' when Type == NumberDisplayType.Accuracy:
                case '7' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[7];
                case '7' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[7];
                case '7' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[7];
                // 8
                case '8' when Type == NumberDisplayType.Score:
                case '8' when Type == NumberDisplayType.Accuracy:
                case '8' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[8];
                case '8' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[8];
                case '8' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[8];
                // 9
                case '9' when Type == NumberDisplayType.Score:
                case '9' when Type == NumberDisplayType.Accuracy:
                case '9' when Type == NumberDisplayType.Rating:
                    return SkinManager.Skin.ScoreDisplayNumbers[9];
                case '9' when Type == NumberDisplayType.Combo:
                    return SkinManager.Skin.ComboDisplayNumbers[9];
                case '9' when Type == NumberDisplayType.SongTime:
                    return SkinManager.Skin.SongTimeDisplayNumbers[9];
                case '.':
                    return SkinManager.Skin.ScoreDisplayDecimal;
                case '%':
                    return SkinManager.Skin.ScoreDisplayPercent;
                case '-':
                    return SkinManager.Skin.SongTimeDisplayMinus;
                case ':':
                    return SkinManager.Skin.SongTimeDisplayColon;
                default:
                    throw new ArgumentException($"Invalid character {c} specified.");
            }
        }
    }

    /// <summary>
    ///     Enum that dictates which type of display it is.
    ///     Some textures are used versus others, so specifying the type here
    ///     allows us to use the correct one.
    /// </summary>
    public enum NumberDisplayType
    {
        Score,
        Accuracy,
        Combo,
        SongTime,
        Rating
    }
}
