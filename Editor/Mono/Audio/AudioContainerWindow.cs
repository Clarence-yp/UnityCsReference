// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Audio;
using UnityEditor.Audio.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace UnityEditor;

sealed class AudioContainerWindow : EditorWindow
{
    /// <summary>
    /// The cached instance of the window, if it is open.
    /// </summary>
    internal static AudioContainerWindow Instance { get; private set; }

    readonly AudioContainerWindowState m_State = new();

    // Preview section
    Label m_AssetNameLabel;
    Button m_PlayButton;
    VisualElement m_PlayButtonImage;
    Button m_SkipButton;
    VisualElement m_SkipButtonImage;

    // Volume section
    Slider m_VolumeSlider;
    AudioRandomRangeSliderTracker m_VolumeRandomRangeTracker;
    FloatField m_VolumeField;
    Button m_VolumeRandomizationButton;
    VisualElement m_VolumeRandomizationButtonImage;
    MinMaxSlider m_VolumeRandomizationRangeSlider;
    Vector2Field m_VolumeRandomizationRangeField;
    AudioLevelMeter m_Meter;

    // Pitch section
    Slider m_PitchSlider;
    AudioRandomRangeSliderTracker m_PitchRandomRangeTracker;
    FloatField m_PitchField;
    Button m_PitchRandomizationButton;
    VisualElement m_PitchRandomizationButtonImage;
    MinMaxSlider m_PitchRandomizationRangeSlider;
    Vector2Field m_PitchRandomizationRangeField;

    // Clip list section
    ListView m_ClipsListView;
    AudioContainerListDragAndDropManipulator m_DragManipulator;

    // Trigger and playback mode section
    RadioButtonGroup m_TriggerRadioButtonGroup;
    RadioButtonGroup m_PlaybackModeRadioButtonGroup;
    IntegerField m_AvoidRepeatingLastField;

    // Automatic trigger section
    RadioButtonGroup m_AutomaticTriggerModeRadioButtonGroup;
    Slider m_TimeSlider;
    AudioRandomRangeSliderTracker m_TimeRandomRangeTracker;
    FloatField m_TimeField;
    Button m_TimeRandomizationButton;
    VisualElement m_TimeRandomizationButtonImage;
    MinMaxSlider m_TimeRandomizationRangeSlider;
    Vector2Field m_TimeRandomizationRangeField;
    RadioButtonGroup m_LoopRadioButtonGroup;
    IntegerField m_CountField;
    Button m_CountRandomizationButton;
    VisualElement m_CountRandomizationButtonImage;
    MinMaxSlider m_CountRandomizationRangeSlider;
    Vector2Field m_CountRandomizationRangeField;
    Label m_AutomaticTriggerModeLabel;
    Label m_LoopLabel;

    // Shared icon references
    Texture2D m_DiceIconOff;
    Texture2D m_DiceIconOn;

    bool m_IsLoading;

    List<AudioContainerElement> m_CachedElements = new();

    [RequiredByNativeCode]
    internal static void CreateAudioRandomContainerWindow()
    {
        var window = GetWindow<AudioContainerWindow>();
        window.Show();
    }

    /// <summary>
    /// Updates the state, which will implicitly refresh the window content if needed.
    /// </summary>
    internal void Refresh()
    {
        m_State.UpdateTarget();
    }

    static void OnCreateButtonClicked()
    {
        ProjectWindowUtil.CreateAudioRandomContainer();
    }

    void OnEnable()
    {
        Instance = this;
        m_State.TargetChanged += OnTargetChanged;
        m_State.TransportStateChanged += OnTransportStateChanged;
        m_State.EditorPauseStateChanged += EditorPauseStateChanged;
        m_DiceIconOff = EditorGUIUtility.IconContent("AudioRandomContainer On Icon").image as Texture2D;
        m_DiceIconOn = EditorGUIUtility.IconContent("AudioRandomContainer Icon").image as Texture2D;
        SetTitle(m_State.IsDirty());
    }

    void OnDisable()
    {
        Instance = null;

        if (m_PlayButton != null)
            m_PlayButton.clicked -= OnPlayStopButtonClicked;

        if (m_SkipButton != null)
            m_SkipButton.clicked -= OnSkipButtonClicked;

        m_VolumeRandomizationRangeSlider?.UnregisterValueChangedCallback(OnVolumeRandomizationRangeChanged);
        m_VolumeRandomizationRangeField?.UnregisterValueChangedCallback(OnVolumeRandomizationRangeChanged);

        m_PitchRandomizationRangeSlider?.UnregisterValueChangedCallback(OnPitchRandomizationRangeChanged);
        m_PitchRandomizationRangeField?.UnregisterValueChangedCallback(OnPitchRandomizationRangeChanged);

        m_TimeRandomizationRangeSlider?.UnregisterValueChangedCallback(OnTimeRandomizationRangeChanged);
        m_TimeRandomizationRangeField?.UnregisterValueChangedCallback(OnTimeRandomizationRangeChanged);

        m_State.TargetChanged -= OnTargetChanged;
        m_State.TransportStateChanged -= OnTransportStateChanged;
        m_State.EditorPauseStateChanged -= EditorPauseStateChanged;
        m_State.OnDestroy();

        m_CachedElements.Clear();
    }

    void Update()
    {
        UpdateClipFieldProgressBars();

        if (m_Meter == null)
            return;

        if (m_State != null)
            m_Meter.Value = m_State.GetMeterValue();
        else
            m_Meter.Value = -80.0f;
    }

    void OnBecameInvisible()
    {
        m_State.Stop();
        ClearClipFieldProgressBars();
    }

    void SetTitle(bool targetIsDirty)
    {
        var titleString = "Audio Random Container";

        if (targetIsDirty)
            titleString += "*";

        titleContent = new GUIContent(titleString)
        {
            image = m_DiceIconOff
        };
    }

    void CreateGUI()
    {
        try
        {
            if (m_IsLoading)
                return;

            m_IsLoading = true;

            rootVisualElement.Unbind();
            rootVisualElement.Clear();

            var rootAsset = UIToolkitUtilities.LoadUxml("UXML/Audio/AudioRandomContainer.uxml");
            rootAsset.CloneTree(rootVisualElement);

            var styleSheet = UIToolkitUtilities.LoadStyleSheet("StyleSheets/Audio/AudioRandomContainer.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            var ARCelement = UIToolkitUtilities.GetChildByName<ScrollView>(rootVisualElement, "ARC_ScrollView");
            var day0Element = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "Day0");

            if (m_State.AudioContainer == null)
            {
                ARCelement.style.display = DisplayStyle.None;
                InitDay0GUI();
                return;
            }

            day0Element.style.display = DisplayStyle.None;

            InitAudioRandomContainerGUI();
            UpdateTransportButtonStates();
        }
        finally
        {
            m_IsLoading = false;
        }
    }

    void InitDay0GUI()
    {
        var day0Element = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "Day0");
        var createButtonLabel = UIToolkitUtilities.GetChildByName<Label>(day0Element, "CreateButtonLabel");
        var createButton = UIToolkitUtilities.GetChildByName<Button>(day0Element, "CreateButton");
        createButton.clicked += OnCreateButtonClicked;
        createButtonLabel.text = "Select an existing Audio Random Container asset in the project browser or create a new one using the button below.";
    }

    void InitAudioRandomContainerGUI()
    {
        InitPreviewGUI();
        InitVolumeGUI();
        InitPitchGUI();
        InitClipListGUI();
        InitTriggerAndPlayModeGUI();
        InitAutomaticTriggerGUI();
        OnTriggerChanged((AudioRandomContainerTriggerMode)m_TriggerRadioButtonGroup.value);
        UpdateTransportButtonStates();
        rootVisualElement.TrackSerializedObjectValue(m_State.SerializedObject, OnSerializedObjectChanged);
        EditorApplication.update += OneTimeEditorApplicationUpdate;
    }

    void OnTargetChanged(object sender, EventArgs e)
    {
        SetTitle(m_State.IsDirty());
        CreateGUI();

        if (m_State.AudioContainer != null)
            m_CachedElements = m_State.AudioContainer.elements.ToList();
    }

    void OnSerializedObjectChanged(SerializedObject obj)
    {
        SetTitle(m_State.IsDirty());
    }

    void OneTimeEditorApplicationUpdate()
    {
        // Setting this is a temp workaround for a UIToolKit bug
        // https://unity.slack.com/archives/C3414V4UV/p1681828689005249?thread_ts=1676901177.340799&cid=C3414V4UV
        m_ClipsListView.reorderable = true;
        m_ClipsListView.reorderMode = ListViewReorderMode.Animated;
        EditorApplication.update -= OneTimeEditorApplicationUpdate;
    }

    #region Preview

    void InitPreviewGUI()
    {
        m_AssetNameLabel = UIToolkitUtilities.GetChildByName<Label>(rootVisualElement, "asset-name-label");
        m_AssetNameLabel.text = m_State.AudioContainer.name;

        m_PlayButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "play-button");
        m_PlayButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "play-button-image");
        m_PlayButton.clicked += OnPlayStopButtonClicked;

        m_SkipButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "skip-button");
        m_SkipButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "skip-button-image");
        m_SkipButton.clicked += OnSkipButtonClicked;
        var skipIcon = UIToolkitUtilities.LoadIcon("icon_next");
        m_SkipButtonImage.style.backgroundImage = new StyleBackground(skipIcon);
    }

    void OnPlayStopButtonClicked()
    {
        if (m_State.IsPlayingOrPaused())
        {
            m_State.Stop();
            ClearClipFieldProgressBars();
        }
        else
            m_State.Play();

        UpdateTransportButtonStates();
    }

    void OnSkipButtonClicked()
    {
        if (m_State.IsPlayingOrPaused())
            m_State.Skip();
    }

    void UpdateTransportButtonStates()
    {
        var editorIsPaused = EditorApplication.isPaused;

        m_PlayButton?.SetEnabled(m_State.IsReadyToPlay() && !editorIsPaused);
        m_SkipButton?.SetEnabled(m_State.IsPlayingOrPaused() && m_State.AudioContainer.triggerMode == AudioRandomContainerTriggerMode.Automatic && !editorIsPaused);

        var image =
            m_State.IsPlayingOrPaused()
                ? UIToolkitUtilities.LoadIcon("icon_stop")
                : UIToolkitUtilities.LoadIcon("icon_play");

        m_PlayButtonImage.style.backgroundImage = new StyleBackground(image);
    }

    void OnTransportStateChanged(object sender, EventArgs e)
    {
        UpdateTransportButtonStates();
    }

    void EditorPauseStateChanged(object sender, EventArgs e)
    {
        UpdateTransportButtonStates();
    }

    #endregion

    #region Volume

    void InitVolumeGUI()
    {
        m_Meter = UIToolkitUtilities.GetChildByName<AudioLevelMeter>(rootVisualElement, "meter");

        var volumeProperty = m_State.SerializedObject.FindProperty("m_Volume");
        var volumeRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_VolumeRandomizationEnabled");
        var volumeRandomizationRangeProperty = m_State.SerializedObject.FindProperty("m_VolumeRandomizationRange");

        m_VolumeSlider = UIToolkitUtilities.GetChildByName<Slider>(rootVisualElement, "volume-slider");
        m_VolumeSlider.BindProperty(volumeProperty);
        m_VolumeRandomRangeTracker = AudioRandomRangeSliderTracker.Create(m_VolumeSlider, m_State.AudioContainer.volumeRandomizationRange);
        m_VolumeSlider.RegisterValueChangedCallback(OnVolumeChanged);

        m_VolumeField = UIToolkitUtilities.GetChildByName<FloatField>(rootVisualElement, "volume-field");
        m_VolumeField.BindProperty(volumeProperty);
        m_VolumeField.formatString = "0.# dB";
        m_VolumeField.isDelayed = true;

        m_VolumeRandomizationButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "volume-randomization-button");
        m_VolumeRandomizationButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "volume-randomization-button-image");
        m_VolumeRandomizationButton.clicked += OnVolumeRandomizationButtonClicked;
        m_VolumeRandomizationButton.TrackPropertyValue(volumeRandomizationEnabledProperty, OnVolumeRandomizationEnabledChanged);

        m_VolumeRandomizationRangeSlider = UIToolkitUtilities.GetChildByName<MinMaxSlider>(rootVisualElement, "volume-randomization-range-slider");
        m_VolumeRandomizationRangeSlider.BindProperty(volumeRandomizationRangeProperty);
        m_VolumeRandomizationRangeSlider.RegisterValueChangedCallback(OnVolumeRandomizationRangeChanged);

        m_VolumeRandomizationRangeField = UIToolkitUtilities.GetChildByName<Vector2Field>(rootVisualElement, "volume-randomization-range-field");
        m_VolumeRandomizationRangeField.BindProperty(volumeRandomizationRangeProperty);
        m_VolumeRandomizationRangeField.RegisterValueChangedCallback(OnVolumeRandomizationRangeChanged);

        var volumeRandomizationMinField = UIToolkitUtilities.GetChildByName<FloatField>(m_VolumeRandomizationRangeField, "unity-x-input");
        volumeRandomizationMinField.isDelayed = true;
        volumeRandomizationMinField.label = "";
        volumeRandomizationMinField.formatString = "0.# dB";

        var volumeRandomizationMaxField = UIToolkitUtilities.GetChildByName<FloatField>(m_VolumeRandomizationRangeField, "unity-y-input");
        volumeRandomizationMaxField.isDelayed = true;
        volumeRandomizationMaxField.label = "";
        volumeRandomizationMaxField.formatString = "0.# dB";

        OnVolumeRandomizationEnabledChanged(volumeRandomizationEnabledProperty);
    }

    void OnVolumeChanged(ChangeEvent<float> evt)
    {
        m_VolumeRandomRangeTracker.SetRange(m_State.AudioContainer.volumeRandomizationRange);
    }

    void OnVolumeRandomizationRangeChanged(ChangeEvent<Vector2> evt)
    {
        // Have to clamp immediately here to avoid UI jitter because the min-max slider cannot clamp before updating the property
        var newValue = evt.newValue;

        if (newValue.x > 0)
            newValue.x = 0;

        if (newValue.y < 0)
            newValue.y = 0;

        m_VolumeRandomRangeTracker.SetRange(newValue);
    }

    void OnVolumeRandomizationEnabledChanged(SerializedProperty property)
    {
        if (property.boolValue)
        {
            m_VolumeRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOn);
            m_VolumeRandomizationRangeSlider.SetEnabled(true);
            m_VolumeRandomizationRangeField.SetEnabled(true);
        }
        else
        {
            m_VolumeRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOff);
            m_VolumeRandomizationRangeSlider.SetEnabled(false);
            m_VolumeRandomizationRangeField.SetEnabled(false);
        }
    }

    void OnVolumeRandomizationButtonClicked()
    {
        var newButtonStateString = !m_State.AudioContainer.volumeRandomizationEnabled ? "Enabled" : "Disabled";
        Undo.RecordObject(m_State.AudioContainer, $"Modified Volume Randomization {newButtonStateString} in {m_State.AudioContainer.name}");
        m_State.AudioContainer.volumeRandomizationEnabled = !m_State.AudioContainer.volumeRandomizationEnabled;
    }

    #endregion

    #region Pitch

    void InitPitchGUI()
    {
        var pitchProperty = m_State.SerializedObject.FindProperty("m_Pitch");
        var pitchRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_PitchRandomizationEnabled");
        var pitchRandomizationRangeProperty = m_State.SerializedObject.FindProperty("m_PitchRandomizationRange");

        m_PitchSlider = UIToolkitUtilities.GetChildByName<Slider>(rootVisualElement, "pitch-slider");
        m_PitchSlider.BindProperty(pitchProperty);
        m_PitchRandomRangeTracker = AudioRandomRangeSliderTracker.Create(m_PitchSlider, m_State.AudioContainer.pitchRandomizationRange);
        m_PitchSlider.RegisterValueChangedCallback(OnPitchChanged);

        m_PitchField = UIToolkitUtilities.GetChildByName<FloatField>(rootVisualElement, "pitch-field");
        m_PitchField.BindProperty(pitchProperty);
        m_PitchField.formatString = "0 ct";
        m_PitchField.isDelayed = true;

        m_PitchRandomizationButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "pitch-randomization-button");
        m_PitchRandomizationButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "pitch-randomization-button-image");
        m_PitchRandomizationButton.clicked += OnPitchRandomizationButtonClicked;
        m_PitchRandomizationButton.TrackPropertyValue(pitchRandomizationEnabledProperty, OnPitchRandomizationEnabledChanged);

        m_PitchRandomizationRangeSlider = UIToolkitUtilities.GetChildByName<MinMaxSlider>(rootVisualElement, "pitch-randomization-range-slider");
        m_PitchRandomizationRangeSlider.BindProperty(pitchRandomizationRangeProperty);
        m_PitchRandomizationRangeSlider.RegisterValueChangedCallback(OnPitchRandomizationRangeChanged);

        m_PitchRandomizationRangeField = UIToolkitUtilities.GetChildByName<Vector2Field>(rootVisualElement, "pitch-randomization-range-field");
        m_PitchRandomizationRangeField.BindProperty(pitchRandomizationRangeProperty);
        m_PitchRandomizationRangeField.RegisterValueChangedCallback(OnPitchRandomizationRangeChanged);

        var pitchRandomizationMinField = UIToolkitUtilities.GetChildByName<FloatField>(m_PitchRandomizationRangeField, "unity-x-input");
        pitchRandomizationMinField.isDelayed = true;
        pitchRandomizationMinField.label = "";
        pitchRandomizationMinField.formatString = "0 ct";

        var pitchRandomizationMaxField = UIToolkitUtilities.GetChildByName<FloatField>(m_PitchRandomizationRangeField, "unity-y-input");
        pitchRandomizationMaxField.isDelayed = true;
        pitchRandomizationMaxField.label = "";
        pitchRandomizationMaxField.formatString = "0 ct";

        OnPitchRandomizationEnabledChanged(pitchRandomizationEnabledProperty);
    }

    void OnPitchChanged(ChangeEvent<float> evt)
    {
        m_PitchRandomRangeTracker.SetRange(m_State.AudioContainer.pitchRandomizationRange);
    }

    void OnPitchRandomizationRangeChanged(ChangeEvent<Vector2> evt)
    {
        // Have to clamp immediately here to avoid UI jitter because the min-max slider cannot clamp before updating the property
        var newValue = evt.newValue;

        if (newValue.x > 0)
            newValue.x = 0;

        if (newValue.y < 0)
            newValue.y = 0;

        m_PitchRandomRangeTracker.SetRange(newValue);
    }

    void OnPitchRandomizationEnabledChanged(SerializedProperty property)
    {
        if (property.boolValue)
        {
            m_PitchRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOn);
            m_PitchRandomizationRangeSlider.SetEnabled(true);
            m_PitchRandomizationRangeField.SetEnabled(true);
        }
        else
        {
            m_PitchRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOff);
            m_PitchRandomizationRangeSlider.SetEnabled(false);
            m_PitchRandomizationRangeField.SetEnabled(false);
        }
    }

    void OnPitchRandomizationButtonClicked()
    {
        var newButtonStateString = !m_State.AudioContainer.pitchRandomizationEnabled ? "Enabled" : "Disabled";
        Undo.RecordObject(m_State.AudioContainer, $"Modified Pitch Randomization {newButtonStateString} in {m_State.AudioContainer.name}");
        m_State.AudioContainer.pitchRandomizationEnabled = !m_State.AudioContainer.pitchRandomizationEnabled;
    }

    #endregion

    #region ClipList

    void InitClipListGUI()
    {
        var clipsProperty = m_State.SerializedObject.FindProperty("m_Elements");

        m_ClipsListView = UIToolkitUtilities.GetChildByName<ListView>(rootVisualElement, "audio-clips-list-view");

        m_ClipsListView.BindProperty(clipsProperty);
        m_ClipsListView.TrackPropertyValue(clipsProperty, OnAudioClipListChanged);
        m_ClipsListView.fixedItemHeight = 24;

        m_ClipsListView.itemsAdded += OnListItemsAdded;
        m_ClipsListView.itemsRemoved += OnListItemsRemoved;
        m_ClipsListView.itemIndexChanged += OnItemListIndexChanged;

        m_ClipsListView.makeItem = OnMakeListItem;
        m_ClipsListView.bindItem = OnBindListItem;
        m_ClipsListView.CreateDragAndDropController();

        m_DragManipulator = new AudioContainerListDragAndDropManipulator(rootVisualElement);
        m_DragManipulator.addAudioClipsDelegate += OnAudioClipDrag;
    }

    static VisualElement OnMakeListItem()
    {
        return UIToolkitUtilities.LoadUxml("UXML/Audio/AudioContainerElement.uxml").Instantiate();
    }

    void OnBindListItem(VisualElement element, int index)
    {
        var listElement = m_State.AudioContainer.elements[index];

        if (listElement == null)
            return;

        var serializedObject = new SerializedObject(listElement);

        var enabledProperty = serializedObject.FindProperty("m_Enabled");
        var audioClipProperty = serializedObject.FindProperty("m_AudioClip");
        var volumeProperty = serializedObject.FindProperty("m_Volume");

        var enabledToggle = UIToolkitUtilities.GetChildByName<Toggle>(element, "enabled-toggle");
        var audioClipField = UIToolkitUtilities.GetChildByName<AudioContainerElementClipField>(element, "audio-clip-field");
        var volumeField = UIToolkitUtilities.GetChildByName<FloatField>(element, "volume-field");
        volumeField.formatString = "0.# dB";

        audioClipField.objectType = typeof(AudioClip);
        audioClipField.RegisterCallback<DragPerformEvent>(evt => { evt.StopPropagation(); });

        audioClipField.AssetElementInstanceID = listElement.GetInstanceID();

        enabledToggle.BindProperty(enabledProperty);
        audioClipField.BindProperty(audioClipProperty);
        volumeField.BindProperty(volumeProperty);

        enabledToggle.TrackPropertyValue(enabledProperty, OnElementEnabledToggleChanged);
        audioClipField.TrackPropertyValue(audioClipProperty, OnElementAudioClipChanged);
        volumeField.TrackPropertyValue(volumeProperty, OnElementPropertyChanged);
    }

    void OnElementAudioClipChanged(SerializedProperty property)
    {
        OnElementPropertyChanged(property);
        UpdateTransportButtonStates();
        m_State.AudioContainer.NotifyObservers(AudioRandomContainer.ChangeEventType.List);
    }

    void OnElementEnabledToggleChanged(SerializedProperty property)
    {
        OnElementPropertyChanged(property);
        UpdateTransportButtonStates();

        // Changing a property on the ListElement subasset does not call CheckConsistency on the main Asset
        // So quickly flip the values to force an update. :(
        var last = m_State.AudioContainer.avoidRepeatingLast;
        m_State.AudioContainer.avoidRepeatingLast = -1;
        m_State.AudioContainer.avoidRepeatingLast = last;

        if (m_State.IsPlayingOrPaused()) m_State.AudioContainer.NotifyObservers(AudioRandomContainer.ChangeEventType.List);
    }

    void OnElementPropertyChanged(SerializedProperty property)
    {
        EditorUtility.SetDirty(m_State.AudioContainer);
        SetTitle(true);
    }

    void OnListItemsAdded(IEnumerable<int> indices)
    {
        var elements = m_State.AudioContainer.elements.ToList();

        foreach (var index in indices.Reverse())
        {
            var element = new AudioContainerElement
            {
                hideFlags = HideFlags.HideInHierarchy
            };
            elements[index] = element;
            AssetDatabase.AddObjectToAsset(element, m_State.AudioContainer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(element, out var guid, out var localId);
            element.name = $"AudioContainerElement_{{{localId}}}";
        }

        m_State.AudioContainer.elements = elements.ToArray();
    }

    void OnListItemsRemoved(IEnumerable<int> indices)
    {
        foreach (var index in indices)
            AssetDatabase.RemoveObjectFromAsset(m_CachedElements[index]);

        m_State.AudioContainer.NotifyObservers(AudioRandomContainer.ChangeEventType.List);
    }

    void OnItemListIndexChanged(int oldIndex, int newIndex)
    {
        m_ClipsListView.Rebuild();
        m_State.AudioContainer.NotifyObservers(AudioRandomContainer.ChangeEventType.List);
    }

    void OnAudioClipDrag(List<AudioClip> audioClips)
    {
        var elements = m_State.AudioContainer.elements.ToList();

        foreach (var audioClip in audioClips)
        {
            var element = new AudioContainerElement
            {
                audioClip = audioClip,
                hideFlags = HideFlags.HideInHierarchy
            };
            elements.Add(element);
            AssetDatabase.AddObjectToAsset(element, m_State.AudioContainer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(element, out var guid, out var localId);
            element.name = $"{audioClip.name}_{{{localId}}}";
        }

        m_State.AudioContainer.elements = elements.ToArray();
    }

    void OnAudioClipListChanged(SerializedProperty property)
    {
        UpdateTransportButtonStates();
        m_CachedElements = m_State.AudioContainer.elements.ToList();
    }

    void UpdateClipFieldProgressBars()
    {
        var playables = m_State.GetActivePlayables();

        if (playables == null)
            return;

        // Iterate over the ActivePlayables from the runtime and try and match them to the instance ID on the clip field.
        // if its a match, set the progress and remove the clip field to avoid overwriting the progress.
        var clipFields = m_ClipsListView.Query<AudioContainerElementClipField>().ToList();

        // We need to sort the active playables as the runtime does not guarantee order
        Array.Sort(playables, (x, y) => x.settings.scheduledTime.CompareTo(y.settings.scheduledTime));

        for (var i = playables.Length - 1; i >= 0; i--)
        {
            var playable = new AudioClipPlayable(playables[i].clipPlayableHandle);

            for (var j = clipFields.Count - 1; j >= 0; j--)
            {
                var field = clipFields[j];

                if (field.AssetElementInstanceID == playables[i].settings.element.GetInstanceID())
                {
                    field.Progress = playable.GetClipPositionSec() / playable.GetClip().length;
                    clipFields.RemoveAt(j);
                }
            }
        }

        // Any clip fields that did not have a match with active playables should have their progress set to 0.
        foreach (var field in clipFields)
            if (field.Progress != 0.0f)
                field.Progress = 0.0f;
    }

    void ClearClipFieldProgressBars()
    {
        if (m_ClipsListView == null)
            return;

        var clipFields = m_ClipsListView.Query<AudioContainerElementClipField>().ToList();

        foreach (var field in clipFields)
            field.Progress = 0.0f;
    }

    #endregion

    #region TriggerAndPlaybackMode

    void InitTriggerAndPlayModeGUI()
    {
        var triggerProperty = m_State.SerializedObject.FindProperty("m_TriggerMode");
        var playbackModeProperty = m_State.SerializedObject.FindProperty("m_PlaybackMode");
        var avoidRepeatingLastProperty = m_State.SerializedObject.FindProperty("m_AvoidRepeatingLast");

        m_TriggerRadioButtonGroup = UIToolkitUtilities.GetChildByName<RadioButtonGroup>(rootVisualElement, "trigger-radio-button-group");
        m_TriggerRadioButtonGroup.BindProperty(triggerProperty);
        m_TriggerRadioButtonGroup.TrackPropertyValue(triggerProperty, OnTriggerChanged);

        m_PlaybackModeRadioButtonGroup = UIToolkitUtilities.GetChildByName<RadioButtonGroup>(rootVisualElement, "playback-radio-button-group");
        m_PlaybackModeRadioButtonGroup.BindProperty(playbackModeProperty);
        m_PlaybackModeRadioButtonGroup.TrackPropertyValue(playbackModeProperty, OnPlaybackModeChanged);

        m_AvoidRepeatingLastField = UIToolkitUtilities.GetChildByName<IntegerField>(rootVisualElement, "avoid-repeating-last-field");
        m_AvoidRepeatingLastField.BindProperty(avoidRepeatingLastProperty);

        OnPlaybackModeChanged(playbackModeProperty);
    }

    void OnTriggerChanged(SerializedProperty property)
    {
        OnTriggerChanged((AudioRandomContainerTriggerMode)property.intValue);
    }

    void OnTriggerChanged(AudioRandomContainerTriggerMode mode)
    {
        var enabled = mode == AudioRandomContainerTriggerMode.Automatic;
        m_AutomaticTriggerModeRadioButtonGroup.SetEnabled(enabled);
        m_TimeSlider.SetEnabled(enabled);
        m_TimeField.SetEnabled(enabled);
        m_LoopRadioButtonGroup.SetEnabled(enabled);
        m_AutomaticTriggerModeLabel.SetEnabled(enabled);
        m_LoopLabel.SetEnabled(enabled);
        m_TimeRandomizationButton.SetEnabled(enabled);
        m_CountRandomizationButton.SetEnabled(enabled);

        var loopProperty = m_State.SerializedObject.FindProperty("m_LoopMode");
        OnLoopChanged(loopProperty);

        var timeRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_AutomaticTriggerTimeRandomizationEnabled");
        OnTimeRandomizationEnabledChanged(timeRandomizationEnabledProperty);
    }

    void OnPlaybackModeChanged(SerializedProperty property)
    {
        m_AvoidRepeatingLastField.SetEnabled(property.intValue == (int)AudioRandomContainerPlaybackMode.Random);
    }

    #endregion

    #region AutomaticTrigger

    void InitAutomaticTriggerGUI()
    {
        var automaticTriggerModeProperty = m_State.SerializedObject.FindProperty("m_AutomaticTriggerMode");
        var triggerTimeProperty = m_State.SerializedObject.FindProperty("m_AutomaticTriggerTime");
        var triggerTimeRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_AutomaticTriggerTimeRandomizationEnabled");
        var triggerTimeRandomizationRangeProperty = m_State.SerializedObject.FindProperty("m_AutomaticTriggerTimeRandomizationRange");
        var loopModeProperty = m_State.SerializedObject.FindProperty("m_LoopMode");
        var loopCountProperty = m_State.SerializedObject.FindProperty("m_LoopCount");
        var loopCountRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_LoopCountRandomizationEnabled");
        var loopCountRandomizationRangeProperty = m_State.SerializedObject.FindProperty("m_LoopCountRandomizationRange");

        m_AutomaticTriggerModeRadioButtonGroup = UIToolkitUtilities.GetChildByName<RadioButtonGroup>(rootVisualElement, "trigger-mode-radio-button-group");
        m_AutomaticTriggerModeRadioButtonGroup.BindProperty(automaticTriggerModeProperty);

        m_TimeSlider = UIToolkitUtilities.GetChildByName<Slider>(rootVisualElement, "time-slider");
        m_TimeSlider.BindProperty(triggerTimeProperty);
        m_TimeRandomRangeTracker = AudioRandomRangeSliderTracker.Create(m_TimeSlider, m_State.AudioContainer.automaticTriggerTimeRandomizationRange);
        m_TimeSlider.RegisterValueChangedCallback(OnTimeChanged);

        m_TimeField = UIToolkitUtilities.GetChildByName<FloatField>(rootVisualElement, "time-field");
        m_TimeField.BindProperty(triggerTimeProperty);
        m_TimeField.formatString = "0.00 s";
        m_TimeField.isDelayed = true;

        m_TimeRandomizationButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "time-randomization-button");
        m_TimeRandomizationButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "time-randomization-button-image");
        m_TimeRandomizationButton.clicked += OnTimeRandomizationButtonClicked;
        m_TimeRandomizationButton.TrackPropertyValue(triggerTimeRandomizationEnabledProperty, OnTimeRandomizationEnabledChanged);

        m_TimeRandomizationRangeSlider = UIToolkitUtilities.GetChildByName<MinMaxSlider>(rootVisualElement, "time-randomization-range-slider");
        m_TimeRandomizationRangeSlider.BindProperty(triggerTimeRandomizationRangeProperty);
        m_TimeRandomizationRangeSlider.RegisterValueChangedCallback(OnTimeRandomizationRangeChanged);

        m_TimeRandomizationRangeField = UIToolkitUtilities.GetChildByName<Vector2Field>(rootVisualElement, "time-randomization-range-field");
        m_TimeRandomizationRangeField.BindProperty(triggerTimeRandomizationRangeProperty);
        m_TimeRandomizationRangeField.RegisterValueChangedCallback(OnTimeRandomizationRangeChanged);

        var timeRandomizationMinField = UIToolkitUtilities.GetChildByName<FloatField>(m_TimeRandomizationRangeField, "unity-x-input");
        timeRandomizationMinField.isDelayed = true;
        timeRandomizationMinField.label = "";
        timeRandomizationMinField.formatString = "0.#";

        var timeRandomizationMaxField = UIToolkitUtilities.GetChildByName<FloatField>(m_TimeRandomizationRangeField, "unity-y-input");
        timeRandomizationMaxField.isDelayed = true;
        timeRandomizationMaxField.label = "";
        timeRandomizationMaxField.formatString = "0.#";

        m_LoopRadioButtonGroup = UIToolkitUtilities.GetChildByName<RadioButtonGroup>(rootVisualElement, "loop-radio-button-group");
        m_LoopRadioButtonGroup.BindProperty(loopModeProperty);
        m_LoopRadioButtonGroup.TrackPropertyValue(loopModeProperty, OnLoopChanged);

        m_CountField = UIToolkitUtilities.GetChildByName<IntegerField>(rootVisualElement, "count-field");
        m_CountField.BindProperty(loopCountProperty);
        m_CountField.formatString = "0.#";
        m_CountField.isDelayed = true;

        m_CountRandomizationButton = UIToolkitUtilities.GetChildByName<Button>(rootVisualElement, "count-randomization-button");
        m_CountRandomizationButtonImage = UIToolkitUtilities.GetChildByName<VisualElement>(rootVisualElement, "count-randomization-button-image");
        m_CountRandomizationButton.clicked += OnCountRandomizationButtonClicked;
        m_CountRandomizationButton.TrackPropertyValue(loopCountRandomizationEnabledProperty, OnCountRandomizationEnabledChanged);

        m_CountRandomizationRangeSlider = UIToolkitUtilities.GetChildByName<MinMaxSlider>(rootVisualElement, "count-randomization-range-slider");
        m_CountRandomizationRangeSlider.BindProperty(loopCountRandomizationRangeProperty);

        m_CountRandomizationRangeField = UIToolkitUtilities.GetChildByName<Vector2Field>(rootVisualElement, "count-randomization-range-field");
        m_CountRandomizationRangeField.BindProperty(loopCountRandomizationRangeProperty);

        var countRandomizationMinField = UIToolkitUtilities.GetChildByName<FloatField>(m_CountRandomizationRangeField, "unity-x-input");
        countRandomizationMinField.isDelayed = true;
        countRandomizationMinField.label = "";

        var countRandomizationMaxField = UIToolkitUtilities.GetChildByName<FloatField>(m_CountRandomizationRangeField, "unity-y-input");
        countRandomizationMaxField.isDelayed = true;
        countRandomizationMaxField.label = "";

        m_AutomaticTriggerModeLabel = UIToolkitUtilities.GetChildByName<Label>(rootVisualElement, "automatic-trigger-mode-label");
        m_LoopLabel = UIToolkitUtilities.GetChildByName<Label>(rootVisualElement, "loop-label");

        OnTimeRandomizationEnabledChanged(triggerTimeRandomizationEnabledProperty);
        OnLoopChanged(loopModeProperty);
        OnCountRandomizationEnabledChanged(loopCountRandomizationEnabledProperty);
    }

    void OnTimeChanged(ChangeEvent<float> evt)
    {
        m_TimeRandomRangeTracker.SetRange(m_State.AudioContainer.automaticTriggerTimeRandomizationRange);
    }

    void OnTimeRandomizationRangeChanged(ChangeEvent<Vector2> evt)
    {
        // Have to clamp immediately here to avoid UI jitter because the min-max slider cannot clamp before updating the property
        var newValue = evt.newValue;

        if (newValue.x > 0)
            newValue.x = 0;

        if (newValue.y < 0)
            newValue.y = 0;

        m_TimeRandomRangeTracker.SetRange(newValue);
    }

    void OnTimeRandomizationEnabledChanged(SerializedProperty property)
    {
        if (property.boolValue
            && m_State.AudioContainer.triggerMode == AudioRandomContainerTriggerMode.Automatic)
        {
            m_TimeRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOn);
            m_TimeRandomizationRangeSlider.SetEnabled(true);
            m_TimeRandomizationRangeField.SetEnabled(true);
        }
        else
        {
            m_TimeRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOff);
            m_TimeRandomizationRangeSlider.SetEnabled(false);
            m_TimeRandomizationRangeField.SetEnabled(false);
        }
    }

    void OnTimeRandomizationButtonClicked()
    {
        var newButtonStateString = !m_State.AudioContainer.automaticTriggerTimeRandomizationEnabled ? "Enabled" : "Disabled";
        Undo.RecordObject(m_State.AudioContainer, $"Modified Time Randomization {newButtonStateString} in {m_State.AudioContainer.name}");
        m_State.AudioContainer.automaticTriggerTimeRandomizationEnabled = !m_State.AudioContainer.automaticTriggerTimeRandomizationEnabled;
    }

    void OnLoopChanged(SerializedProperty property)
    {
        var enabled = property.intValue != (int)AudioRandomContainerLoopMode.Infinite && m_State.AudioContainer.triggerMode == AudioRandomContainerTriggerMode.Automatic;

        m_CountField.SetEnabled(enabled);
        m_CountRandomizationRangeSlider.SetEnabled(enabled);
        m_CountRandomizationRangeField.SetEnabled(enabled);
        m_CountRandomizationButton.SetEnabled(enabled);

        var countRandomizationEnabledProperty = m_State.SerializedObject.FindProperty("m_LoopCountRandomizationEnabled");
        OnCountRandomizationEnabledChanged(countRandomizationEnabledProperty);
    }

    void OnCountRandomizationEnabledChanged(SerializedProperty property)
    {
        if (property.boolValue
            && m_State.AudioContainer.loopMode != AudioRandomContainerLoopMode.Infinite
            && m_State.AudioContainer.triggerMode == AudioRandomContainerTriggerMode.Automatic)
        {
            m_CountRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOn);
            m_CountRandomizationRangeSlider.SetEnabled(true);
            m_CountRandomizationRangeField.SetEnabled(true);
        }
        else
        {
            m_CountRandomizationButtonImage.style.backgroundImage = new StyleBackground(m_DiceIconOff);
            m_CountRandomizationRangeSlider.SetEnabled(false);
            m_CountRandomizationRangeField.SetEnabled(false);
        }
    }

    void OnCountRandomizationButtonClicked()
    {
        var newButtonStateString = !m_State.AudioContainer.loopCountRandomizationEnabled ? "Enabled" : "Disabled";
        Undo.RecordObject(m_State.AudioContainer, $"Modified Count Randomization {newButtonStateString} in {m_State.AudioContainer.name}");
        m_State.AudioContainer.loopCountRandomizationEnabled = !m_State.AudioContainer.loopCountRandomizationEnabled;
    }

    #endregion

    #region GlobalEditorCallbackHandlers

    void OnWillSaveAssets(IEnumerable<string> paths)
    {
        if (m_State.AudioContainer == null)
            return;

        var currentSelectionPath = AssetDatabase.GetAssetPath(m_State.AudioContainer);

        foreach (var path in paths)
            if (path == currentSelectionPath)
            {
                SetTitle(false);
                return;
            }
    }

    void OnAssetsImported(IEnumerable<string> paths)
    {
        if (m_State.AudioContainer == null) return;

        foreach (var path in paths)
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AudioRandomContainer) &&
                AssetDatabase.GetMainAssetInstanceID(path) == m_State.AudioContainer.GetInstanceID())
            {
                m_State.SerializedObject.Update();
                OnTargetChanged(this, EventArgs.Empty);
            }
    }

    void OnAssetsDeleted(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            if (path == m_State.TargetPath)
            {
                m_State.Reset();
                SetTitle(false);
                CreateGUI();
                m_CachedElements.Clear();
                break;
            }
    }

    class AudioContainerModificationProcessor : AssetModificationProcessor
    {
        /// <summary>
        /// Handles save of AudioRandomContainer assets
        /// and relays it to AudioContainerWindow,
        /// removing the asterisk in the window tab label.
        /// </summary>
        static string[] OnWillSaveAssets(string[] paths)
        {
            if (Instance != null)
                Instance.OnWillSaveAssets(paths);

            return paths;
        }
    }

    class AudioContainerPostProcessor : AssetPostprocessor
    {
        /// <summary>
        /// Handles import and deletion of AudioRandomContainer assets
        /// and relays it to AudioContainerWindow,
        /// refreshing or clearing the window content.
        /// </summary>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (Instance == null)
                return;

            if (importedAssets.Length > 0)
                Instance.OnAssetsImported(importedAssets);

            if (deletedAssets.Length > 0)
                Instance.OnAssetsDeleted(deletedAssets);
        }
    }

    #endregion
}
