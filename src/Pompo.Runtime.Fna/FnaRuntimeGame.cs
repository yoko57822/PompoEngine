using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pompo.Core.Assets;
using Pompo.Core.Localization;
using Pompo.Core.Project;
using Pompo.Core.Runtime;
using Pompo.VisualScripting;
using Pompo.Runtime.Fna.Presentation;
using Pompo.VisualScripting.Runtime;

namespace Pompo.Runtime.Fna;

public sealed class FnaRuntimeGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly PompoGraphIR? _ir;
    private readonly IReadOnlyDictionary<string, PompoGraphIR>? _graphLibrary;
    private readonly IRuntimeCustomNodeHandler? _customNodeHandler;
    private readonly RuntimeAssetCatalog? _assetCatalog;
    private readonly StringTableLocalizer? _localizer;
    private readonly string? _saveRoot;
    private readonly RuntimeSaveStore _saveStore = new();
    private readonly RuntimeUiLayout _uiLayout;
    private readonly TinyBitmapFont _font = new();
    private readonly RuntimeAudioPlayer _audioPlayer = new();
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly RuntimeUiThemeColors _theme;
    private readonly PompoRuntimeUiSkin _runtimeUiSkin;
    private readonly PompoRuntimeUiAnimationSettings _runtimeUiAnimation;
    private readonly PompoRuntimePlaybackSettings _runtimePlayback;
    private readonly RuntimeTextReveal _textReveal;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private RuntimePlaySession? _playSession;
    private RuntimeUiFrame _uiFrame;
    private string? _statusMessage;
    private bool _showBacklog;
    private string _selectedSaveSlotId = "manual_1";
    private bool _autoForward;
    private bool _skipMode;
    private double _autoTimerSeconds;
    private double _skipTimerSeconds;
    private double _runtimeSeconds;

    public FnaRuntimeGame()
        : this(CreateDefaultFrame())
    {
    }

    public FnaRuntimeGame(
        RuntimeUiFrame uiFrame,
        RuntimeAssetCatalog? assetCatalog = null,
        PompoGraphIR? ir = null,
        IReadOnlyDictionary<string, PompoGraphIR>? graphLibrary = null,
        StringTableLocalizer? localizer = null,
        string? saveRoot = null,
        IRuntimeCustomNodeHandler? customNodeHandler = null,
        PompoRuntimeUiTheme? runtimeUiTheme = null,
        PompoRuntimeUiSkin? runtimeUiSkin = null,
        PompoRuntimeUiLayoutSettings? runtimeUiLayout = null,
        PompoRuntimeUiAnimationSettings? runtimeUiAnimation = null,
        PompoRuntimePlaybackSettings? runtimePlayback = null)
    {
        _uiFrame = uiFrame;
        _theme = RuntimeUiThemeColors.FromProjectTheme(runtimeUiTheme);
        _runtimeUiSkin = runtimeUiSkin ?? new PompoRuntimeUiSkin();
        _runtimeUiAnimation = runtimeUiAnimation ?? new PompoRuntimeUiAnimationSettings();
        _runtimePlayback = runtimePlayback ?? new PompoRuntimePlaybackSettings();
        _textReveal = new RuntimeTextReveal(_runtimeUiAnimation);
        _uiLayout = new RuntimeUiLayout(runtimeUiLayout);
        _assetCatalog = assetCatalog;
        _ir = ir;
        _graphLibrary = graphLibrary;
        _customNodeHandler = customNodeHandler;
        _localizer = localizer;
        _saveRoot = saveRoot;
        _selectedSaveSlotId = uiFrame.SaveMenu?.Slots.FirstOrDefault(slot => slot.IsSelected)?.SlotId ?? "manual_1";
        if (ir is not null)
        {
            _playSession = new RuntimePlaySession(
                ir,
                graphLibrary,
                localizer,
                ListSaveSlots(),
                _selectedSaveSlotId,
                customNodeHandler,
                runtimeUiLayout);
            _uiFrame = _playSession.Frame;
        }

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        Window.Title = "PompoEngine FNA Runtime";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        LoadFrameTextures();
        _audioPlayer.Apply(_uiFrame.Audio, _assetCatalog);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        _textReveal.Update(_uiFrame.Dialogue?.Text, gameTime.ElapsedGameTime.TotalSeconds);
        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (WasPressed(keyboard, Keys.A))
        {
            _autoForward = !_autoForward;
            _skipMode = false;
            ResetAdvanceTimers();
            SetStatus(_autoForward ? "Auto-forward on" : "Auto-forward off");
        }

        if (WasPressed(keyboard, Keys.S))
        {
            _skipMode = !_skipMode;
            _autoForward = false;
            ResetAdvanceTimers();
            SetStatus(_skipMode ? "Skip on" : "Skip off");
        }

        if (WasPressed(keyboard, Keys.F5))
        {
            QuickSave();
        }

        if (WasPressed(keyboard, Keys.F6))
        {
            SaveSelectedSlot();
        }

        if (WasPressed(keyboard, Keys.F9))
        {
            QuickLoad();
        }

        if (WasPressed(keyboard, Keys.Enter) && CompleteTextReveal())
        {
        }
        else if (WasPressed(keyboard, Keys.Enter) && _playSession?.HasChoices == true)
        {
            AdvanceRuntime();
        }
        else if (WasPressed(keyboard, Keys.Enter) && _uiFrame.SaveMenu is not null)
        {
            LoadSelectedSlot();
        }

        if (WasPressed(keyboard, Keys.Space) && CompleteTextReveal())
        {
        }
        else if (WasPressed(keyboard, Keys.Space))
        {
            AdvanceRuntime();
        }

        if (TryGetVirtualMousePosition(mouse, out var virtualX, out var virtualY))
        {
            if (UpdateSaveSlotPointer(virtualX, virtualY, WasLeftClicked(mouse)))
            {
            }
            else
            {
                UpdatePointerHover(virtualX, virtualY);
                if (WasLeftClicked(mouse) && !CompleteTextReveal())
                {
                    AdvanceRuntimeAt(virtualX, virtualY);
                }
            }
        }

        if (WasPressed(keyboard, Keys.Up) && _playSession?.HasChoices == true)
        {
            _playSession.SelectChoiceRelative(-1);
            _uiFrame = _playSession.Frame;
        }
        else if (WasPressed(keyboard, Keys.Up))
        {
            SelectRelativeSaveSlot(-1);
        }

        if (WasPressed(keyboard, Keys.Down) && _playSession?.HasChoices == true)
        {
            _playSession.SelectChoiceRelative(1);
            _uiFrame = _playSession.Frame;
        }
        else if (WasPressed(keyboard, Keys.Down))
        {
            SelectRelativeSaveSlot(1);
        }

        if (WasPressed(keyboard, Keys.B))
        {
            _showBacklog = !_showBacklog && _uiFrame.Backlog is not null;
        }

        _runtimeSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        TickAutoAdvance(gameTime);
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_theme.CanvasClear);
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var canvas = CalculateLetterbox(viewport.Width, viewport.Height, 1920, 1080);
        var scaleX = canvas.Width / (float)_uiFrame.VirtualWidth;
        var scaleY = canvas.Height / (float)_uiFrame.VirtualHeight;

        _spriteBatch.Begin();
        _spriteBatch.Draw(_pixel, canvas, _theme.StageFallback);
        DrawStage(_spriteBatch, _pixel, canvas, scaleX, scaleY);
        var animation = RuntimeUiAnimationTiming.Evaluate(_runtimeUiAnimation, _runtimeSeconds);
        if (_uiFrame.Dialogue is not null)
        {
            DrawSkinnedRect(
                _spriteBatch,
                _pixel,
                canvas,
                scaleX,
                scaleY,
                _uiFrame.Dialogue.TextBox,
                _theme.DialogueBackground,
                _runtimeUiSkin.DialogueBox,
                animation.PanelOpacity);
            DrawSkinnedRect(
                _spriteBatch,
                _pixel,
                canvas,
                scaleX,
                scaleY,
                _uiFrame.Dialogue.NameBox,
                _theme.NameBoxBackground,
                _runtimeUiSkin.NameBox,
                animation.PanelOpacity);
            DrawDialogueText(_spriteBatch, _pixel, canvas, scaleX, scaleY, _uiFrame.Dialogue);
            foreach (var choice in _uiFrame.Dialogue.Choices)
            {
                var choiceBounds = choice.IsSelected
                    ? ScaleAroundCenter(choice.Bounds, animation.SelectedChoiceScale)
                    : choice.Bounds;
                DrawSkinnedRect(
                    _spriteBatch,
                    _pixel,
                    canvas,
                    scaleX,
                    scaleY,
                    choiceBounds,
                    ChoiceBackgroundFor(choice),
                    ChoiceSkinFor(choice),
                    animation.PanelOpacity);
                DrawTextVirtual(
                    _spriteBatch,
                    _pixel,
                    canvas,
                    scaleX,
                    scaleY,
                    choice.Text,
                    choice.Bounds.X + 24,
                    choice.Bounds.Y + 17,
                    4,
                    choice.IsEnabled ? _theme.Text : _theme.MutedText);
            }
        }

        if (_uiFrame.SaveMenu is not null)
        {
            DrawSaveMenu(_spriteBatch, _pixel, canvas, scaleX, scaleY, _uiFrame.SaveMenu);
        }

        if (_showBacklog && _uiFrame.Backlog is not null)
        {
            DrawBacklog(_spriteBatch, _pixel, canvas, scaleX, scaleY, _uiFrame.Backlog);
        }

        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            DrawTextVirtual(
                _spriteBatch,
                _pixel,
                canvas,
                scaleX,
                scaleY,
                _statusMessage,
                60,
                1010,
                3,
                _theme.MutedText);
        }
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }

        _textures.Clear();
        _audioPlayer.Dispose();
        _pixel?.Dispose();
        base.UnloadContent();
    }

    private void DrawStage(SpriteBatch spriteBatch, Texture2D pixel, Rectangle canvas, float scaleX, float scaleY)
    {
        var backgroundColor = string.IsNullOrWhiteSpace(_uiFrame.BackgroundAssetId)
            ? _theme.StageFallback
            : _theme.StageActiveFallback;
        if (TryGetTexture(_uiFrame.BackgroundAssetId, out var background))
        {
            spriteBatch.Draw(background, canvas, Color.White);
        }
        else
        {
            spriteBatch.Draw(pixel, canvas, backgroundColor);
        }

        if (!string.IsNullOrWhiteSpace(_uiFrame.BackgroundAssetId))
        {
            DrawTextVirtual(
                spriteBatch,
                pixel,
                canvas,
                scaleX,
                scaleY,
                $"BG {_uiFrame.BackgroundAssetId}",
                60,
                60,
                4,
                _theme.MutedText);
        }

        foreach (var character in _uiFrame.Characters.Where(character => character.Visible).OrderBy(character => character.Layer))
        {
            DrawCharacterPlaceholder(spriteBatch, pixel, canvas, scaleX, scaleY, character);
        }
    }

    private bool WasPressed(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private bool WasLeftClicked(MouseState mouse)
    {
        return mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
    }

    private void TickAutoAdvance(GameTime gameTime)
    {
        if (_playSession is null || _playSession.HasChoices || _showBacklog)
        {
            ResetAdvanceTimers();
            return;
        }

        if (!_textReveal.IsComplete(_uiFrame.Dialogue?.Text))
        {
            if (_skipMode)
            {
                CompleteTextReveal();
            }

            ResetAdvanceTimers();
            return;
        }

        if (_autoForward)
        {
            _autoTimerSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            if (_autoTimerSeconds >= _runtimePlayback.AutoForwardDelayMilliseconds / 1000d)
            {
                _autoTimerSeconds = 0;
                AdvanceRuntime();
            }
        }

        if (_skipMode)
        {
            _skipTimerSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            if (_skipTimerSeconds >= _runtimePlayback.SkipIntervalMilliseconds / 1000d)
            {
                _skipTimerSeconds = 0;
                AdvanceRuntime();
            }
        }
    }

    private void ResetAdvanceTimers()
    {
        _autoTimerSeconds = 0;
        _skipTimerSeconds = 0;
    }

    private void AdvanceRuntime()
    {
        if (_playSession is null)
        {
            return;
        }

        try
        {
            _playSession.AdvanceOrChoose();
            _uiFrame = _playSession.Frame;
            LoadFrameTextures();
            _audioPlayer.Apply(_uiFrame.Audio, _assetCatalog);
            if (_uiFrame.Dialogue?.Choices.Count > 0)
            {
                _autoForward = false;
                _skipMode = false;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            SetStatus($"Advance failed: {ex.Message}");
        }
    }

    private bool CompleteTextReveal()
    {
        return _textReveal.Complete(_uiFrame.Dialogue?.Text);
    }

    private void UpdatePointerHover(int virtualX, int virtualY)
    {
        if (_playSession?.SelectChoiceAtVirtualPoint(virtualX, virtualY) == true)
        {
            _uiFrame = _playSession.Frame;
        }
    }

    private bool UpdateSaveSlotPointer(int virtualX, int virtualY, bool clicked)
    {
        var slotId = RuntimeSaveMenuHitTest.GetSlotIdAt(_uiFrame.SaveMenu, virtualX, virtualY);
        if (slotId is null)
        {
            return false;
        }

        if (IsManualSlotId(slotId) && !string.Equals(_selectedSaveSlotId, slotId, StringComparison.Ordinal))
        {
            _selectedSaveSlotId = slotId;
            RefreshSaveMenu();
        }

        if (clicked)
        {
            LoadSlot(slotId, string.Equals(slotId, "quick", StringComparison.Ordinal) ? "Quick loaded" : $"Loaded {slotId}");
        }

        return true;
    }

    private void AdvanceRuntimeAt(int virtualX, int virtualY)
    {
        if (_playSession is null)
        {
            return;
        }

        try
        {
            _playSession.ChooseAtVirtualPoint(virtualX, virtualY);
            _uiFrame = _playSession.Frame;
            LoadFrameTextures();
            _audioPlayer.Apply(_uiFrame.Audio, _assetCatalog);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            SetStatus($"Advance failed: {ex.Message}");
        }
    }

    private void QuickSave()
    {
        if (string.IsNullOrWhiteSpace(_saveRoot) || _uiFrame.CurrentSaveData is null)
        {
            SetStatus("Save unavailable");
            return;
        }

        try
        {
            _saveStore
                .SaveAsync(_saveRoot, "quick", "Quick Save", _uiFrame.CurrentSaveData)
                .GetAwaiter()
                .GetResult();
            RefreshSaveMenu();
            SetStatus("Quick saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private void QuickLoad()
    {
        LoadSlot("quick", "Quick loaded");
    }

    private void SaveSelectedSlot()
    {
        if (string.IsNullOrWhiteSpace(_saveRoot) || _uiFrame.CurrentSaveData is null)
        {
            SetStatus("Save unavailable");
            return;
        }

        var slot = _uiFrame.SaveMenu?.Slots.FirstOrDefault(item => string.Equals(item.SlotId, _selectedSaveSlotId, StringComparison.Ordinal));
        var displayName = string.IsNullOrWhiteSpace(slot?.DisplayName) ? _selectedSaveSlotId : slot.DisplayName;
        try
        {
            _saveStore
                .SaveAsync(_saveRoot, _selectedSaveSlotId, displayName, _uiFrame.CurrentSaveData)
                .GetAwaiter()
                .GetResult();
            RefreshSaveMenu();
            SetStatus($"Saved {_selectedSaveSlotId}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
    }

    private void LoadSelectedSlot()
    {
        LoadSlot(_selectedSaveSlotId, $"Loaded {_selectedSaveSlotId}");
    }

    private void LoadSlot(string slotId, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(_saveRoot) || _ir is null)
        {
            SetStatus("Load unavailable");
            return;
        }

        if (!HasSaveData(slotId))
        {
            SetStatus($"Slot '{slotId}' is empty");
            return;
        }

        try
        {
            var saveFile = _saveStore
                .LoadAsync(_saveRoot, slotId)
                .GetAwaiter()
                .GetResult();
            var runtime = _graphLibrary is null
                ? GraphRuntimeInterpreter.FromSaveData(_ir, saveFile.Data, _localizer)
                : GraphRuntimeInterpreter.FromSaveData(_graphLibrary, saveFile.Data, _localizer);
            if (_playSession is not null)
            {
                _playSession.UpdateSaveSlots(ListSaveSlots(), _selectedSaveSlotId);
                _playSession.RestoreFromSaveData(saveFile.Data);
                _uiFrame = _playSession.Frame;
            }
            else
            {
                _uiFrame = _uiLayout.CreateFrame(
                    runtime.Snapshot,
                    ListSaveSlots(),
                    saveFile.Data,
                    _selectedSaveSlotId);
            }

            _showBacklog = false;
            LoadFrameTextures();
            _audioPlayer.Apply(_uiFrame.Audio, _assetCatalog);
            SetStatus(successMessage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
    }

    private bool HasSaveData(string slotId)
    {
        if (_uiFrame.SaveMenu is null)
        {
            return true;
        }

        if (string.Equals(slotId, _uiFrame.SaveMenu.QuickSlot.SlotId, StringComparison.Ordinal))
        {
            return !_uiFrame.SaveMenu.QuickSlot.IsEmpty;
        }

        var slot = _uiFrame.SaveMenu.Slots.FirstOrDefault(item => string.Equals(item.SlotId, slotId, StringComparison.Ordinal));
        return slot is null || !slot.IsEmpty;
    }

    private void SelectRelativeSaveSlot(int offset)
    {
        if (_uiFrame.SaveMenu is null)
        {
            return;
        }

        var current = Math.Clamp(ParseManualSlotIndex(_selectedSaveSlotId) - 1, 0, 5);
        var next = (current + offset + 6) % 6;
        _selectedSaveSlotId = $"manual_{next + 1}";
        RefreshSaveMenu();
    }

    private void SetStatus(string message)
    {
        _statusMessage = message.Length <= 96
            ? message
            : string.Concat(message.AsSpan(0, 93), "...");
    }

    private void RefreshSaveMenu()
    {
        var saveSlots = ListSaveSlots();
        if (_playSession is not null)
        {
            _playSession.UpdateSaveSlots(saveSlots, _selectedSaveSlotId);
            _uiFrame = _playSession.Frame;
            return;
        }

        _uiFrame = _uiLayout.WithSaveMenu(_uiFrame, saveSlots, _selectedSaveSlotId);
    }

    private IReadOnlyList<RuntimeSaveSlotMetadata>? ListSaveSlots()
    {
        if (string.IsNullOrWhiteSpace(_saveRoot))
        {
            return null;
        }

        return _saveStore
            .ListAsync(_saveRoot)
            .GetAwaiter()
            .GetResult();
    }

    private static int ParseManualSlotIndex(string slotId)
    {
        return slotId.StartsWith("manual_", StringComparison.Ordinal) &&
            int.TryParse(slotId.AsSpan("manual_".Length), out var index)
            ? index
            : 1;
    }

    private static bool IsManualSlotId(string slotId)
    {
        return slotId is "manual_1" or "manual_2" or "manual_3" or "manual_4" or "manual_5" or "manual_6";
    }

    private bool TryGetVirtualMousePosition(MouseState mouse, out int virtualX, out int virtualY)
    {
        virtualX = 0;
        virtualY = 0;
        var viewport = GraphicsDevice.Viewport;
        var canvas = CalculateLetterbox(viewport.Width, viewport.Height, _uiFrame.VirtualWidth, _uiFrame.VirtualHeight);
        if (!canvas.Contains(mouse.X, mouse.Y))
        {
            return false;
        }

        virtualX = (int)((mouse.X - canvas.X) / (canvas.Width / (float)_uiFrame.VirtualWidth));
        virtualY = (int)((mouse.Y - canvas.Y) / (canvas.Height / (float)_uiFrame.VirtualHeight));
        return true;
    }

    private void DrawCharacterPlaceholder(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        RuntimeCharacterState character)
    {
        var width = 260;
        var height = 480;
        var x = (int)((character.X * _uiFrame.VirtualWidth) - (width / 2f));
        var y = (int)((character.Y * _uiFrame.VirtualHeight) - height);
        var rect = new UiRect(
            Math.Clamp(x, 40, _uiFrame.VirtualWidth - width - 40),
            Math.Clamp(y, 120, _uiFrame.VirtualHeight - height - 260),
            width,
            height);
        var color = character.Layer switch
        {
            RuntimeLayer.CharacterBack => new Color(74, 96, 128, 210),
            RuntimeLayer.CharacterFront => new Color(96, 120, 160, 235),
            _ => new Color(82, 110, 150, 225)
        };

        var spritePath = _assetCatalog?.ResolveCharacterSpritePath(character.CharacterId, character.ExpressionId);
        if (spritePath is not null && TryGetTexturePath(spritePath, out var sprite))
        {
            DrawTextureFit(spriteBatch, sprite, canvas, scaleX, scaleY, rect);
        }
        else
        {
            DrawRect(spriteBatch, pixel, canvas, scaleX, scaleY, rect, color);
        }
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            character.CharacterId,
            rect.X + 22,
            rect.Y + 28,
            4,
            _theme.Text);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            character.ExpressionId,
            rect.X + 22,
            rect.Y + 76,
            3,
            _theme.MutedText);
    }

    private void LoadFrameTextures()
    {
        if (_assetCatalog is null)
        {
            return;
        }

        LoadTexture(_assetCatalog.ResolveAssetPath(_uiFrame.BackgroundAssetId));
        foreach (var character in _uiFrame.Characters)
        {
            LoadTexture(_assetCatalog.ResolveCharacterSpritePath(character.CharacterId, character.ExpressionId));
        }

        foreach (var assetRef in GetRuntimeUiSkinRefs(_runtimeUiSkin))
        {
            LoadTexture(_assetCatalog.ResolveAssetPath(assetRef.AssetId));
        }
    }

    private void LoadTexture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            _textures.ContainsKey(path) ||
            !File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            _textures[path] = Texture2D.FromStream(GraphicsDevice, stream);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"Could not load runtime texture '{path}': {ex.Message}");
        }
    }

    private bool TryGetTexture(string? assetId, out Texture2D texture)
    {
        texture = null!;
        var path = _assetCatalog?.ResolveAssetPath(assetId);
        return path is not null && _textures.TryGetValue(path, out texture!);
    }

    private bool TryGetTexturePath(string path, out Texture2D texture)
    {
        return _textures.TryGetValue(path, out texture!);
    }

    private static void DrawTextureFit(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        UiRect rect)
    {
        var target = new Rectangle(
            canvas.X + (int)(rect.X * scaleX),
            canvas.Y + (int)(rect.Y * scaleY),
            (int)(rect.Width * scaleX),
            (int)(rect.Height * scaleY));
        var imageAspect = texture.Width / (float)texture.Height;
        var targetAspect = target.Width / (float)target.Height;

        if (imageAspect > targetAspect)
        {
            var height = (int)(target.Width / imageAspect);
            target = new Rectangle(target.X, target.Y + ((target.Height - height) / 2), target.Width, height);
        }
        else
        {
            var width = (int)(target.Height * imageAspect);
            target = new Rectangle(target.X + ((target.Width - width) / 2), target.Y, width, target.Height);
        }

        spriteBatch.Draw(texture, target, Color.White);
    }

    private void DrawDialogueText(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        RuntimeDialogueUi dialogue)
    {
        if (!string.IsNullOrWhiteSpace(dialogue.Speaker))
        {
            DrawTextVirtual(
                spriteBatch,
                pixel,
                canvas,
                scaleX,
                scaleY,
                dialogue.Speaker,
                dialogue.NameBox.X + 24,
                dialogue.NameBox.Y + 17,
                4,
                _theme.Text);
        }

        var fontScale = 5;
        var visibleText = _textReveal.GetVisibleText(dialogue.Text);
        var maxCharacters = Math.Max(12, dialogue.TextBox.Width / ((5 + 1) * fontScale));
        var lines = RuntimeTextLayout.Wrap(visibleText, maxCharacters, 3);
        var lineHeight = (7 + 2) * fontScale;
        for (var index = 0; index < lines.Count; index++)
        {
            DrawTextVirtual(
                spriteBatch,
                pixel,
                canvas,
                scaleX,
                scaleY,
                lines[index],
                dialogue.TextBox.X + 28,
                dialogue.TextBox.Y + 28 + (index * lineHeight),
                fontScale,
                _theme.Text);
        }
    }

    private void DrawSaveMenu(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        RuntimeSaveMenuUi saveMenu)
    {
        DrawSkinnedRect(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            saveMenu.Bounds,
            _theme.SaveMenuBackground,
            _runtimeUiSkin.SaveMenuPanel);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            saveMenu.Title,
            saveMenu.Bounds.X + 28,
            saveMenu.Bounds.Y + 28,
            4,
            _theme.Text);

        if (saveMenu.Slots.Count == 0)
        {
            DrawTextVirtual(
                spriteBatch,
                pixel,
                canvas,
                scaleX,
                scaleY,
                "No save slots",
                saveMenu.Bounds.X + 28,
                saveMenu.Bounds.Y + 96,
                4,
                _theme.MutedText);
            return;
        }

        DrawSaveSlot(spriteBatch, pixel, canvas, scaleX, scaleY, saveMenu.QuickSlot);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            "Manual Slots",
            saveMenu.Bounds.X + 28,
            saveMenu.Bounds.Y + 166,
            3,
            _theme.MutedText);

        foreach (var slot in saveMenu.Slots)
        {
            DrawSaveSlot(spriteBatch, pixel, canvas, scaleX, scaleY, slot);
        }

        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            saveMenu.HelpText,
            saveMenu.Bounds.X + 28,
            saveMenu.Bounds.Bottom - 44,
            2,
            _theme.HelpText);
    }

    private void DrawSaveSlot(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        RuntimeSaveSlotUi slot)
    {
        var color = slot.IsSelected
            ? _theme.ChoiceSelectedBackground
            : slot.IsEmpty
                ? _theme.SaveSlotEmptyBackground
                : _theme.SaveSlotBackground;
        var skinAsset = slot.IsSelected
            ? _runtimeUiSkin.SaveSlotSelected
            : slot.IsEmpty
                ? _runtimeUiSkin.SaveSlotEmpty
                : _runtimeUiSkin.SaveSlot;
        DrawSkinnedRect(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            slot.Bounds,
            color,
            skinAsset);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            $"{slot.DisplayName}  {slot.SlotId}",
            slot.Bounds.X + 18,
            slot.Bounds.Y + 10,
            3,
            slot.IsEmpty ? _theme.MutedText : _theme.Text);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            $"{slot.Location}  {slot.SavedAt}",
            slot.Bounds.X + 18,
            slot.Bounds.Y + 36,
            2,
            _theme.HelpText);
    }

    private void DrawBacklog(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        RuntimeBacklogUi backlog)
    {
        DrawSkinnedRect(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            backlog.Bounds,
            _theme.BacklogBackground,
            _runtimeUiSkin.BacklogPanel);
        DrawTextVirtual(
            spriteBatch,
            pixel,
            canvas,
            scaleX,
            scaleY,
            backlog.Title,
            backlog.Bounds.X + 32,
            backlog.Bounds.Y + 30,
            4,
            _theme.Text);

        var y = backlog.Bounds.Y + 92;
        foreach (var line in backlog.Lines)
        {
            var speaker = string.IsNullOrWhiteSpace(line.Speaker) ? "Narration" : line.Speaker;
            DrawTextVirtual(
                spriteBatch,
                pixel,
                canvas,
                scaleX,
                scaleY,
                speaker,
                backlog.Bounds.X + 32,
                y,
                3,
                _theme.AccentText);

            var wrapped = RuntimeTextLayout.Wrap(line.Text, 72, 2);
            for (var index = 0; index < wrapped.Count; index++)
            {
                DrawTextVirtual(
                    spriteBatch,
                    pixel,
                    canvas,
                    scaleX,
                    scaleY,
                    wrapped[index],
                    backlog.Bounds.X + 210,
                    y + (index * 28),
                    3,
                    _theme.Text);
            }

            y += 72;
        }
    }

    private void DrawTextVirtual(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        string text,
        int virtualX,
        int virtualY,
        int virtualScale,
        Color color)
    {
        var screenScale = Math.Max(1, (int)(virtualScale * Math.Min(scaleX, scaleY)));
        _font.DrawText(
            spriteBatch,
            pixel,
            text,
            canvas.X + (int)(virtualX * scaleX),
            canvas.Y + (int)(virtualY * scaleY),
            screenScale,
            color);
    }

    private static void DrawRect(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        UiRect rect,
        Color color)
    {
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                canvas.X + (int)(rect.X * scaleX),
                canvas.Y + (int)(rect.Y * scaleY),
                (int)(rect.Width * scaleX),
                (int)(rect.Height * scaleY)),
            color);
    }

    private static UiRect ScaleAroundCenter(
        UiRect rect,
        float scale)
    {
        if (scale <= 1f)
        {
            return rect;
        }

        var width = (int)MathF.Round(rect.Width * scale);
        var height = (int)MathF.Round(rect.Height * scale);
        return new UiRect(
            rect.X - ((width - rect.Width) / 2),
            rect.Y - ((height - rect.Height) / 2),
            width,
            height);
    }

    private Color ChoiceBackgroundFor(RuntimeChoiceUi choice)
    {
        if (!choice.IsEnabled)
        {
            return _theme.SaveSlotEmptyBackground;
        }

        return choice.IsSelected ? _theme.ChoiceSelectedBackground : _theme.ChoiceBackground;
    }

    private PompoAssetRef? ChoiceSkinFor(RuntimeChoiceUi choice)
    {
        if (!choice.IsEnabled)
        {
            return _runtimeUiSkin.ChoiceDisabledBox ?? _runtimeUiSkin.ChoiceBox;
        }

        return choice.IsSelected ? _runtimeUiSkin.ChoiceSelectedBox : _runtimeUiSkin.ChoiceBox;
    }

    private void DrawSkinnedRect(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle canvas,
        float scaleX,
        float scaleY,
        UiRect rect,
        Color fallbackColor,
        PompoAssetRef? skinAsset,
        float opacity = 1f)
    {
        var tint = ApplyOpacity(Color.White, opacity);
        if (skinAsset is not null && TryGetTexture(skinAsset.AssetId, out var texture))
        {
            DrawTextureNineSlice(
                spriteBatch,
                texture,
                new Rectangle(
                    canvas.X + (int)(rect.X * scaleX),
                    canvas.Y + (int)(rect.Y * scaleY),
                    (int)(rect.Width * scaleX),
                    (int)(rect.Height * scaleY)),
                tint);
            return;
        }

        DrawRect(spriteBatch, pixel, canvas, scaleX, scaleY, rect, ApplyOpacity(fallbackColor, opacity));
    }

    private static void DrawTextureNineSlice(
        SpriteBatch spriteBatch,
        Texture2D texture,
        Rectangle target,
        Color tint)
    {
        var border = Math.Min(16, Math.Min(texture.Width, texture.Height) / 3);
        border = Math.Min(border, Math.Min(target.Width, target.Height) / 3);
        if (border <= 0 ||
            texture.Width <= border * 2 ||
            texture.Height <= border * 2 ||
            target.Width <= border * 2 ||
            target.Height <= border * 2)
        {
            spriteBatch.Draw(texture, target, tint);
            return;
        }

        var sourceColumns = new[]
        {
            new SliceSegment(0, border),
            new SliceSegment(border, texture.Width - (border * 2)),
            new SliceSegment(texture.Width - border, border)
        };
        var sourceRows = new[]
        {
            new SliceSegment(0, border),
            new SliceSegment(border, texture.Height - (border * 2)),
            new SliceSegment(texture.Height - border, border)
        };
        var targetColumns = new[]
        {
            new SliceSegment(target.X, border),
            new SliceSegment(target.X + border, target.Width - (border * 2)),
            new SliceSegment(target.Right - border, border)
        };
        var targetRows = new[]
        {
            new SliceSegment(target.Y, border),
            new SliceSegment(target.Y + border, target.Height - (border * 2)),
            new SliceSegment(target.Bottom - border, border)
        };

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                if (sourceColumns[column].Length <= 0 ||
                    sourceRows[row].Length <= 0 ||
                    targetColumns[column].Length <= 0 ||
                    targetRows[row].Length <= 0)
                {
                    continue;
                }

                spriteBatch.Draw(
                    texture,
                    new Rectangle(
                        targetColumns[column].Start,
                        targetRows[row].Start,
                        targetColumns[column].Length,
                        targetRows[row].Length),
                    new Rectangle(
                        sourceColumns[column].Start,
                        sourceRows[row].Start,
                        sourceColumns[column].Length,
                        sourceRows[row].Length),
                    tint);
            }
        }
    }

    private static Color ApplyOpacity(
        Color color,
        float opacity)
    {
        var clamped = Math.Clamp(opacity, 0f, 1f);
        return new Color(
            color.R,
            color.G,
            color.B,
            (byte)Math.Clamp((int)MathF.Round(color.A * clamped), 0, 255));
    }

    private readonly record struct SliceSegment(int Start, int Length);

    private static IEnumerable<PompoAssetRef> GetRuntimeUiSkinRefs(PompoRuntimeUiSkin skin)
    {
        if (skin.DialogueBox is not null) yield return skin.DialogueBox;
        if (skin.NameBox is not null) yield return skin.NameBox;
        if (skin.ChoiceBox is not null) yield return skin.ChoiceBox;
        if (skin.ChoiceSelectedBox is not null) yield return skin.ChoiceSelectedBox;
        if (skin.ChoiceDisabledBox is not null) yield return skin.ChoiceDisabledBox;
        if (skin.SaveMenuPanel is not null) yield return skin.SaveMenuPanel;
        if (skin.SaveSlot is not null) yield return skin.SaveSlot;
        if (skin.SaveSlotSelected is not null) yield return skin.SaveSlotSelected;
        if (skin.SaveSlotEmpty is not null) yield return skin.SaveSlotEmpty;
        if (skin.BacklogPanel is not null) yield return skin.BacklogPanel;
    }

    private static Rectangle CalculateLetterbox(int windowWidth, int windowHeight, int virtualWidth, int virtualHeight)
    {
        var scale = Math.Min(windowWidth / (float)virtualWidth, windowHeight / (float)virtualHeight);
        var width = (int)(virtualWidth * scale);
        var height = (int)(virtualHeight * scale);
        return new Rectangle((windowWidth - width) / 2, (windowHeight - height) / 2, width, height);
    }

    private static RuntimeUiFrame CreateDefaultFrame()
    {
        var result = new RuntimeTraceResult(
            "preview",
            false,
            [
                new RuntimeTraceEvent(
                    "line",
                "preview",
                0,
                "Dialogue, narration, choices, and runtime state preview render here.",
                    "Pompo")
            ],
            new Dictionary<string, object?>(),
            null,
            [],
            new RuntimeAudioState(null, []),
            []);
        return new RuntimeUiLayout().CreateFrame(result);
    }
}
