using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Quaver.Assets;
using Quaver.Config;
using Quaver.Database.Maps;
using Quaver.Database.Scores;
using Quaver.Graphics.Notifications;
using Quaver.Logging;
using Quaver.Scheduling;
using Quaver.Screens.Menu;
using Quaver.Skinning;
using Wobble;
using Wobble.Graphics;
using Wobble.Graphics.UI.Debugging;
using Wobble.Input;
using Wobble.Screens;
using Wobble.Window;

namespace Quaver
{
    public class QuaverGame : WobbleGame
    {
        /// <inheritdoc />
        /// <summary>
        /// </summary>
        protected override bool IsReadyToUpdate { get; set; }

        /// <inheritdoc />
        /// <summary>
        ///     Allows the game to perform any initialization it needs to before starting to run.
        ///     This is where it can query for any required services and load any non-graphic
        ///     related content.  Calling base.Initialize will enumerate through any components
        ///     and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            PerformGameSetup();

            WindowManager.ChangeVirtualScreenSize(new Vector2(1366, 768));
            WindowManager.ChangeScreenResolution(new Point(ConfigManager.WindowWidth.Value, ConfigManager.WindowHeight.Value));

            // Unlock the framerate of the game to unlimited.
            Graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
            Graphics.ApplyChanges();

            Window.AllowUserResizing = true;

            base.Initialize();
        }

         /// <inheritdoc />
        /// <summary>
        ///     LoadContent will be called once per game and is the place to load
        ///     all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            base.LoadContent();

            // Load all game assets.
            FontAwesome.Load();
            Fonts.Load();
            Titles.Load();
            UserInterface.Load();

            // Load the user's skin
            SkinManager.Load();

            // Initialize the logger now that we have fonts loaded
            Logger.Initialize();

            // Create the global FPS counter.
            CreateFpsCounter();

            IsReadyToUpdate = true;
            ScreenManager.ChangeScreen(new MainMenuScreen());
        }

        /// <inheritdoc />
        /// <summary>
        ///     UnloadContent will be called once per game and is the place to unload
        ///     game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            base.UnloadContent();
        }

        /// <inheritdoc />
        /// <summary>
        ///     Allows the game to run logic such as updating the world,
        ///     checking for collisions, gathering input, and playing audio.
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            if (!IsReadyToUpdate)
                return;

            base.Update(gameTime);

            // Run scheduled background tasks
            CommonTaskScheduler.Run();
            NotificationManager.Update(gameTime);
        }

        /// <inheritdoc />
        /// <summary>
        ///     This is called when the game should draw itself.
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            if (!IsReadyToUpdate)
                return;

            base.Draw(gameTime);

            NotificationManager.Draw(gameTime);

            GameBase.DefaultSpriteBatchOptions.Begin();
            Logger.Draw(gameTime);
            SpriteBatch.End();

            // Draw the global container last.
            GlobalUserInterface.Draw(gameTime);
        }

        /// <summary>
        ///     Performs any initial setup the game needs to run.
        /// </summary>
        private static void PerformGameSetup()
        {
            ConfigManager.Initialize();
            DeleteTemporaryFiles();

            LocalScoreCache.CreateTable();
            MapCache.LoadAndSetMapsets();

            // Force garabge collection.
            GC.Collect();

            // Start watching for mapset changes in the folder.
            MapsetImporter.WatchForChanges();
        }

        /// <summary>
        ///     Deletes all of the temporary files for the game if they exist.
        /// </summary>
        private static void DeleteTemporaryFiles()
        {
            try
            {
                foreach (var file in new DirectoryInfo(ConfigManager.DataDirectory + "/temp/").GetFiles("*", SearchOption.AllDirectories))
                    file.Delete();

                foreach (var dir in new DirectoryInfo(ConfigManager.DataDirectory + "/temp/").GetDirectories("*", SearchOption.AllDirectories))
                    dir.Delete(true);
            }
            catch (Exception)
            {
                // ignored
            }

            // Create a directory that displays the "Now playing" song.
            Directory.CreateDirectory($"{ConfigManager.DataDirectory}/temp/Now Playing");
        }

        /// <summary>
        ///     Creates the FPS counter to display on a global state.
        /// </summary>
        private void CreateFpsCounter()
        {
            var fpsCounter = new FpsCounter(Fonts.AllerBold16, 0.80f)
            {
                Parent = GlobalUserInterface,
                Alignment = Alignment.BotRight,
                Size = new ScalableVector2(70, 30),
                TextFps =
                {
                    TextColor = Color.LimeGreen
                },
                X = -10,
                Y = -10,
                Alpha = 0
            };

            ShowFpsCounter(fpsCounter);
            ConfigManager.FpsCounter.ValueChanged += (o, e) => ShowFpsCounter(fpsCounter);
        }

        /// <summary>
        ///     Shows the FPs counter based on the current config variable.
        /// </summary>
        private static void ShowFpsCounter(FpsCounter counter) => counter.TextFps.Alpha = ConfigManager.FpsCounter.Value ? 1 : 0;
    }
}
