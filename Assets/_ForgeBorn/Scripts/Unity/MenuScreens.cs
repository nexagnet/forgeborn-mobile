using System;
using UnityEngine;
using UnityEngine.UI;
using static ForgeBorn.UnityLayer.HudWidgets;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Main menu, pause menu and settings screen (GAME_DESIGN §13). Each is a full-screen dim layer with a centred box; the dim layer
    /// keeps raycastTarget on so the workshop HUD behind it cannot be clicked. All game decisions are delegated through Actions.
    /// </summary>
    internal sealed class MenuScreens
    {
        public sealed class Actions
        {
            public Action Resume, Continue, NewGame, Quit;
            public Func<bool> HasSave;
        }

        private readonly GameSettings _settings;
        private readonly Actions _actions;
        private GameObject _main, _pause, _settingsPanel;
        private Text _continueLabel, _resLabel, _fsLabel, _tierLabel, _langLabel, _volLabel;
        private bool _settingsFromMain;

        public bool MainOpen { get { return _main.activeSelf; } }
        public bool PauseOpen { get { return _pause.activeSelf; } }
        public bool SettingsOpen { get { return _settingsPanel.activeSelf; } }
        public bool AnyOpen { get { return MainOpen || PauseOpen || SettingsOpen; } }

        public MenuScreens(RectTransform safe, GameSettings settings, Actions actions)
        {
            _settings = settings;
            _actions = actions;
            BuildMain(safe);
            BuildPause(safe);
            BuildSettings(safe);
            HideAll();
        }

        public void HideAll()
        {
            _main.SetActive(false);
            _pause.SetActive(false);
            _settingsPanel.SetActive(false);
        }

        public void ShowMain()
        {
            HideAll();
            SetEnabled(_continueLabel, _actions.HasSave != null && _actions.HasSave());
            _main.SetActive(true);
        }

        public void ShowPause()
        {
            HideAll();
            _pause.SetActive(true);
        }

        /// <summary>Esc inside a menu: settings go back, pause resumes, the main menu ignores it.</summary>
        public void Back()
        {
            if (SettingsOpen) CloseSettings();
            else if (PauseOpen) _actions.Resume();
        }

        private void OpenSettings(bool fromMain)
        {
            _settingsFromMain = fromMain;
            _main.SetActive(false);
            _pause.SetActive(false);
            RefreshSettings();
            _settingsPanel.SetActive(true);
        }

        private void CloseSettings()
        {
            _settingsPanel.SetActive(false);
            if (_settingsFromMain) ShowMain(); else ShowPause();
        }

        // ---------------------------------------------------------------- layout helpers

        internal static RectTransform Layer(RectTransform safe, string name, string title, int titleSize, Vector2 boxMin, Vector2 boxMax, out GameObject layer)
        {
            layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            var dim = layer.GetComponent<RectTransform>();
            dim.SetParent(safe, false);
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            layer.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            box.SetParent(dim, false);
            box.anchorMin = boxMin;
            box.anchorMax = boxMax;
            box.offsetMin = box.offsetMax = Vector2.zero;
            box.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

            Text t = MakeText(box, "Title", titleSize, TextAnchor.MiddleCenter, new Vector2(0f, 0.84f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            t.text = title;
            t.color = new Color(1f, 0.72f, 0.3f);
            return box;
        }

        private const float RowH = 0.115f, RowGap = 0.025f, RowTop = 0.80f;

        internal static Vector2 RowMin(int row, float x0) { return new Vector2(x0, RowTop - row * (RowH + RowGap) - RowH); }
        internal static Vector2 RowMax(int row, float x1) { return new Vector2(x1, RowTop - row * (RowH + RowGap)); }

        // ---------------------------------------------------------------- screens

        private void BuildMain(RectTransform safe)
        {
            RectTransform box = Layer(safe, "MainMenu", "FORGEBORN", 52, new Vector2(0.34f, 0.2f), new Vector2(0.66f, 0.8f), out _main);
            _continueLabel = MakeButton(box, Loc.T("Continue"), RowMin(0, 0.1f), RowMax(0, 0.9f), () => _actions.Continue());
            MakeButton(box, Loc.T("New game"), RowMin(1, 0.1f), RowMax(1, 0.9f), () => _actions.NewGame());
            MakeButton(box, Loc.T("Settings"), RowMin(2, 0.1f), RowMax(2, 0.9f), () => OpenSettings(true));
            MakeButton(box, Loc.T("Quit"), RowMin(3, 0.1f), RowMax(3, 0.9f), () => _actions.Quit());
        }

        private void BuildPause(RectTransform safe)
        {
            RectTransform box = Layer(safe, "PauseMenu", Loc.T("PAUSED"), 40, new Vector2(0.34f, 0.2f), new Vector2(0.66f, 0.8f), out _pause);
            MakeButton(box, Loc.T("Resume"), RowMin(0, 0.1f), RowMax(0, 0.9f), () => _actions.Resume());
            MakeButton(box, Loc.T("Settings"), RowMin(1, 0.1f), RowMax(1, 0.9f), () => OpenSettings(false));
            MakeButton(box, Loc.T("Main menu"), RowMin(2, 0.1f), RowMax(2, 0.9f), ShowMain);
            MakeButton(box, Loc.T("Quit"), RowMin(3, 0.1f), RowMax(3, 0.9f), () => _actions.Quit());
        }

        private void BuildSettings(RectTransform safe)
        {
            RectTransform box = Layer(safe, Loc.T("Settings"), Loc.T("SETTINGS"), 40, new Vector2(0.30f, 0.12f), new Vector2(0.70f, 0.88f), out _settingsPanel);
            _resLabel = MakeButton(box, "", RowMin(0, 0.08f), RowMax(0, 0.92f), () =>
            {
                _settings.ResolutionIndex = (_settings.ResolutionIndex + 1) % GameSettings.Resolutions.Length;
                Changed();
            });
            _fsLabel = MakeButton(box, "", RowMin(1, 0.08f), RowMax(1, 0.92f), () => { _settings.Fullscreen = !_settings.Fullscreen; Changed(); });
            _tierLabel = MakeButton(box, "", RowMin(2, 0.08f), RowMax(2, 0.49f), () =>
            {
                _settings.Tier = _settings.Tier >= 2 ? -1 : _settings.Tier + 1;
                Changed();
            });
            _langLabel = MakeButton(box, "", RowMin(2, 0.51f), RowMax(2, 0.92f), () =>
            {
                _settings.Lang = (int)(_settings.ResolveLanguage() == Language.Vietnamese ? Language.English : Language.Vietnamese);
                Changed();
            });
            MakeButton(box, Loc.T("Volume -"), RowMin(3, 0.08f), RowMax(3, 0.36f), () => { _settings.Volume -= 0.1f; Changed(); });
            _volLabel = MakeText(box, "VolumeText", 22, TextAnchor.MiddleCenter, RowMin(3, 0.36f), RowMax(3, 0.64f), Vector2.zero, Vector2.zero);
            MakeButton(box, Loc.T("Volume +"), RowMin(3, 0.64f), RowMax(3, 0.92f), () => { _settings.Volume += 0.1f; Changed(); });
            Text note = MakeText(box, "Note", 18, TextAnchor.MiddleCenter, RowMin(4, 0.08f), RowMax(4, 0.92f), Vector2.zero, Vector2.zero);
            note.text = Loc.T("Quality and language take effect after restart.");
            note.color = new Color(0.75f, 0.75f, 0.75f);
            MakeButton(box, Loc.T("Back"), RowMin(5, 0.08f), RowMax(5, 0.92f), CloseSettings);
        }

        private void Changed()
        {
            _settings.Clamp();
            _settings.Apply();
            _settings.Save();
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            Vector2Int r = GameSettings.Resolutions[_settings.ResolutionIndex];
            _resLabel.text = Loc.F("Window size: {0} x {1}", r.x, r.y);
            _fsLabel.text = Loc.F("Fullscreen: {0}", Loc.T(_settings.Fullscreen ? "On" : "Off"));
            _tierLabel.text = Loc.F("Quality: {0}", GameSettings.TierName(_settings.Tier));
            _langLabel.text = Loc.F("Language: {0}", Loc.LanguageName(_settings.ResolveLanguage()));
            _volLabel.text = Mathf.RoundToInt(_settings.Volume * 100f) + "%";
        }
    }
}
