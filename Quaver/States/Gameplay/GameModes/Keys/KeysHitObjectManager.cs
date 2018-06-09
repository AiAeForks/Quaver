﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quaver.API.Enums;
using Quaver.API.Maps.Processors.Scoring.Data;
using Quaver.Config;
using Quaver.Database.Maps;
using Quaver.Graphics.Sprites;
using Quaver.Main;
using Quaver.States.Gameplay.GameModes.Keys.Playfield;
using Quaver.States.Gameplay.HitObjects;

namespace Quaver.States.Gameplay.GameModes.Keys
{
    internal class KeysHitObjectManager : HitObjectManager
    {
        /// <summary>
        ///     Reference to the entire ruleset.
        /// </summary>
        private GameModeRulesetKeys Ruleset { get; }

        /// <summary>
        ///     The list of currently dead notes
        /// </summary>
        internal List<HitObject> DeadNotes { get; }

        /// <summary>
        ///     The list of currently held long notes.
        /// </summary>
        internal List<HitObject> HeldLongNotes { get; }

        /// <summary>
        ///     The speed at which objects fall down from the screen.
        /// </summary>
        internal static float ScrollSpeed {
            get
            {
                var speed = GameBase.SelectedMap.Qua.Mode  == GameMode.Keys4 ? ConfigManager.ScrollSpeed4K : ConfigManager.ScrollSpeed7K;
                return speed.Value / (20f * GameBase.AudioEngine.PlaybackRate);
            }          
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        internal override int ObjectsLeft => ObjectPool.Count + HeldLongNotes.Count + DeadNotes.Count;
        
        /// <summary>
        ///     Dictates if we are currently using downscroll or not.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal static bool IsDownscroll
        {
            get
            {
                switch (GameBase.SelectedMap.Qua.Mode)
                {
                    case GameMode.Keys4:
                        return ConfigManager.DownScroll4K.Value;
                    case GameMode.Keys7:
                        return ConfigManager.DownScroll7K.Value;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        ///     The offset of the hit position.
        /// </summary>
        internal float HitPositionOffset
        {
            get
            {
                var playfield = (KeysPlayfield) Ruleset.Playfield;
                var skin = GameBase.Skin.Keys[Ruleset.Mode];
                
                if (IsDownscroll)
                    return playfield.ReceptorPositionY + (ConfigManager.UserHitPositionOffset4K.Value + skin.HitPosOffsetY);

                // Up Scroll
                return playfield.ReceptorPositionY - (ConfigManager.UserHitPositionOffset4K.Value + skin.HitPosOffsetY) + skin.ColumnSize;
            }
        }

        /// <summary>
        ///     The time it takes for a dead note to be removed after missing it.
        /// </summary>
        private uint DeadNoteRemovalTime { get; }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="ruleset"></param>
        /// <param name="size"></param>
        internal KeysHitObjectManager(GameModeRulesetKeys ruleset, int size) : base(size)
        {
            Ruleset = ruleset;

            DeadNotes = new List<HitObject>();
            HeldLongNotes = new List<HitObject>();
            
            // Set the dead note removal time.
            DeadNoteRemovalTime = (uint)(1000 * GameBase.AudioEngine.PlaybackRate);
        }

        /// <summary>
        ///     Gets the index of the nearest object in a given lane.
        /// </summary>
        /// <param name="lane"></param>
        /// <param name="songTime"></param>
        /// <returns></returns>
        public int GetIndexOfNearestLaneObject(int lane, double songTime)
        {
            // Search for closest ManiaHitObject that is inside the HitTiming Window
            for (var i = 0; i < PoolSize && i < ObjectPool.Count; i++)
            {
                if (ObjectPool[i].Info.Lane == lane && ObjectPool[i].Info.StartTime - songTime > -Ruleset.ScoreProcessor.JudgementWindow[Judgement.Okay])
                    return i;
            }

            // Search held long notes as well for the time being.
            for (var i = 0; i < HeldLongNotes.Count; i++)
            {
                if (HeldLongNotes[i].Info.Lane == lane && HeldLongNotes[i].Info.EndTime - songTime > -Ruleset.ScoreProcessor.JudgementWindow[Judgement.Okay] * Ruleset.ScoreProcessor.WindowReleaseMultiplier[Judgement.Okay])
                    return i;
            }
            
            return -1;
        }

        /// <inheritdoc />
        /// <summary>
        ///     Updates all of the containing notes.
        ///
        ///     Controls things like:
        ///         - Updating the position of objects
        ///         - Detecting if a user has missed object presses or releases. 
        ///     
        /// </summary>
        /// <param name="dt"></param>
        internal override void Update(double dt)
        {            
            UpdateAndScoreActiveObjects();
            UpdateAndScoreHeldObjects();
            UpdateDeadObjects();
        }

        /// <summary>
        ///     Updates the active objects in the pool + adds to score when applicable.
        /// </summary>
        private void UpdateAndScoreActiveObjects()
        {
            // Update active objects.
            for (var i = 0; i < ObjectPool.Count && i < PoolSize; i++)
            {
                var hitObject = (KeysHitObject) ObjectPool[i];
                
                // If the user misses the note.
                if (Ruleset.Screen.Timing.CurrentTime > hitObject.TrueStartTime + Ruleset.ScoreProcessor.JudgementWindow[Judgement.Okay])
                {
                    // Add a miss to their score.
                    Ruleset.ScoreProcessor.CalculateScore(Judgement.Miss);
                    
                    // Update all the users on the scoreboard.
                    Ruleset.Screen.UI.UpdateScoreboardUsers();
                    
                    // Add new hit stat data.
                    var stat = new HitStat(HitStatType.Miss, hitObject.Info, Ruleset.Screen.Timing.CurrentTime, Judgement.Miss, 
                                            int.MinValue, Ruleset.ScoreProcessor.Accuracy, Ruleset.ScoreProcessor.Health);
                    Ruleset.ScoreProcessor.Stats.Add(stat);
                    
                    // Make the combo display visible since it is now changing.
                    var playfield = (KeysPlayfield) Ruleset.Playfield;
                    playfield.Stage.ComboDisplay.MakeVisible();
                    
                    // Perform hit burst animation
                    playfield.Stage.JudgementHitBurst.PerformJudgementAnimation(Judgement.Miss);
                    
                    // If ManiaHitObject is an LN, kill it and count it as another miss because of the tail.
                    if (hitObject.IsLongNote)
                    {
                        KillPoolObject(i);
                        Ruleset.ScoreProcessor.CalculateScore(Judgement.Miss);
                        
                        // Add a duplicate stat since it's an LN, and it counts as two misses.
                        Ruleset.ScoreProcessor.Stats.Add(stat);
                        
                        // Update all the users on the scoreboard.
                        Ruleset.Screen.UI.UpdateScoreboardUsers();
                    }
                    // Otherwise recycle the object.
                    else
                    {
                        RecyclePoolObject(i);
                    }

                    i--;
                }
                // The note is still active and ready to be updated.
                else
                {
                    // Set new HitObject positions.
                    hitObject.PositionY = hitObject.GetPosFromOffset(hitObject.OffsetYFromReceptor);
                    hitObject.UpdateSpritePositions();
                }
            }        
        }

        /// <summary>
        ///     Updates the held long note objects in the pool + adds to score when applicable.
        /// </summary>
        private void UpdateAndScoreHeldObjects()
        {
            // The release window. (Window * Multiplier)
            var window = Ruleset.ScoreProcessor.JudgementWindow[Judgement.Okay] * Ruleset.ScoreProcessor.WindowReleaseMultiplier[Judgement.Okay];

            // Update the currently held long notes.
            for (var i = 0; i < HeldLongNotes.Count; i++)
            {
                var hitObject = (KeysHitObject) HeldLongNotes[i];

                // If the LN's release was missed. (Counts as an okay instead of a miss.)
                if (Ruleset.Screen.Timing.CurrentTime > hitObject.TrueEndTime + window)
                {
                    // The judgement that is given when a user completely misses the release.
                    const Judgement missedJudgement = Judgement.Okay;
   
                    // Calc new score.
                    Ruleset.ScoreProcessor.CalculateScore(missedJudgement);
                    
                    // Add new hit stat data.
                    var stat = new HitStat(HitStatType.Miss, hitObject.Info, Ruleset.Screen.Timing.CurrentTime, Judgement.Okay, 
                                                int.MinValue, Ruleset.ScoreProcessor.Accuracy, Ruleset.ScoreProcessor.Health);
                    Ruleset.ScoreProcessor.Stats.Add(stat);
                    
                    // Update all the users on the scoreboard.
                    Ruleset.Screen.UI.UpdateScoreboardUsers();
                    
                    // Make the combo display visible since it is now changing.
                    var playfield = (KeysPlayfield) Ruleset.Playfield;
                    playfield.Stage.ComboDisplay.MakeVisible();
                    
                    // Perform hit burst animation
                    playfield.Stage.JudgementHitBurst.PerformJudgementAnimation(missedJudgement);

                    // Stop the hitlighting animation.
                    playfield.Stage.HitLighting[hitObject.Info.Lane - 1].StopHolding();
                    
                    // Remove from the queue of long notes.
                    hitObject.Destroy();
                    HeldLongNotes.RemoveAt(i);
                    i--;
                }
                // The long note is still active and ready to be updated.
                else
                {
                    // Start looping the long note body.
                    if (!hitObject.LongNoteBodySprite.IsLooping && !Ruleset.Screen.IsPaused)
                        hitObject.LongNoteBodySprite.StartLoop(LoopDirection.Forward, 30);
                    // If it is looping however and the game is paused, we'll want to stop the loop.
                    else if (hitObject.LongNoteBodySprite.IsLooping && Ruleset.Screen.IsPaused)
                        hitObject.LongNoteBodySprite.StopLoop();
                        
                    // Set the long note size and position.
                    if (Ruleset.Screen.Timing.CurrentTime > hitObject.TrueStartTime)
                    {
                        hitObject.CurrentLongNoteSize = (ulong) ((hitObject.LongNoteOffsetYFromReceptor - Ruleset.Screen.Timing.CurrentTime) * ScrollSpeed);
                        hitObject.PositionY = HitPositionOffset;
                    }
                    else
                    {
                        hitObject.CurrentLongNoteSize = hitObject.InitialLongNoteSize;
                        hitObject.PositionY = hitObject.GetPosFromOffset(hitObject.OffsetYFromReceptor);
                    }

                    // Update the sprite positions of the object.
                    hitObject.UpdateSpritePositions();
                }
            }       
        }

        /// <summary>
        ///     Updates all of the dead objects in the pool.
        /// </summary>
        private void UpdateDeadObjects()
        {
            // Update dead objects.
            for (var i = 0; i < DeadNotes.Count; i++)
            {
                var hitObject = (KeysHitObject) DeadNotes[i];
                
                // Stop looping the animation.
                if (hitObject.IsLongNote && hitObject.LongNoteBodySprite.IsLooping)
                    hitObject.LongNoteBodySprite.StopLoop();
                
                // If the note is past the time of removal, then destroy it.
                if (Ruleset.Screen.Timing.CurrentTime > hitObject.TrueEndTime + DeadNoteRemovalTime 
                    && Ruleset.Screen.Timing.CurrentTime > hitObject.TrueStartTime + DeadNoteRemovalTime)
                {
                    // Remove from dead notes pool.
                    hitObject.Destroy();
                    DeadNotes.RemoveAt(i);
                    i--;
                }
                // Otherwise keep updating it accordingly.
                else
                {
                    hitObject.PositionY = hitObject.GetPosFromOffset(hitObject.OffsetYFromReceptor);
                    hitObject.UpdateSpritePositions();
                }
            }
        }
        
        /// <summary>
        ///     Kills a note at a specific index of the object pool.
        /// </summary>
        /// <param name="index"></param>
        internal void KillPoolObject(int index)
        {
            var hitObject = (KeysHitObject) ObjectPool[index];
            
            // Change the sprite color to dead.
            hitObject.ChangeSpriteColorToDead();
            
            // Add the object to the dead notes pool.
            DeadNotes.Add(hitObject);
            
            // Remove the old HitObject
            ObjectPool.RemoveAt(index);

            //Initialize the new ManiaHitObject (create the hit object sprites)
            InitializeNewPoolObject();
        }

        /// <summary>
        ///     Initializes a new note in the pool.
        /// </summary>
        private void InitializeNewPoolObject()
        {
            if (ObjectPool.Count >= PoolSize)
                ObjectPool[PoolSize - 1].InitializeSprite(Ruleset.Playfield);
        }

        /// <summary>
        ///     Recycles a pool object.
        /// </summary>
        /// <param name="index"></param>
        internal void RecyclePoolObject(int index)
        {
            // Destroy and remove the old object.
            ObjectPool[index].Destroy();
            ObjectPool.RemoveAt(index);
            
            // Initialize a new one in its place at the end.
            InitializeNewPoolObject();
        }

        /// <summary>
        ///     Changes a pool object to a long note that is held at the receptors.
        /// </summary>
        /// <param name="index"></param>
        internal void ChangePoolObjectStatusToHeld(int index)
        {
            // Add to the held long notes.
            HeldLongNotes.Add(ObjectPool[index]);
            
            // Remove the one from the object pool
            ObjectPool.RemoveAt(index);
            
            // Initialize the new note.
            InitializeNewPoolObject();          
        }
        
        /// <summary>
        ///     Kills a hold pool object.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="destroy"></param>
        internal void KillHoldPoolObject(int index, bool destroy = false)
        {
            var hitObject = (KeysHitObject) HeldLongNotes[index];
            
            // Change start time and y offset.
            // TODO: This.
            hitObject.TrueStartTime = (float) Ruleset.Screen.Timing.CurrentTime;
            hitObject.OffsetYFromReceptor = hitObject.TrueStartTime;
            
            if (destroy)
            {
                hitObject.Destroy();
            }
            else
            {
                hitObject.ChangeSpriteColorToDead();
            }
            
            // Remove from the hold pool
            HeldLongNotes.RemoveAt(index);
            DeadNotes.Add(hitObject);
        }
    }
}