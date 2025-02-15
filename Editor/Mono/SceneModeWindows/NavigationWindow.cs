// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEditor.AI;
using UnityEditorInternal;
using EditorNavMeshBuilder = UnityEditor.AI.NavMeshBuilder;
using Object = UnityEngine.Object;
using System.Globalization;

namespace UnityEditor
{
    [EditorWindowTitle(title = "Navigation (Obsolete)", icon = "Navigation")]
    internal class NavMeshEditorWindow : EditorWindow, IHasCustomMenu
    {
        static NavMeshEditorWindow s_NavMeshEditorWindow;

        // Scene based bake configuration
        SerializedObject m_SettingsObject;
        SerializedProperty m_AgentRadius;
        SerializedProperty m_AgentHeight;
        SerializedProperty m_AgentSlope;
        SerializedProperty m_AgentClimb;
        SerializedProperty m_LedgeDropHeight;
        SerializedProperty m_MaxJumpAcrossDistance;

        // .. advanced
        SerializedProperty m_MinRegionArea;
        SerializedProperty m_ManualCellSize;
        SerializedProperty m_CellSize;
        SerializedProperty m_BuildHeightMesh;

        // Project based configuration
        SerializedObject m_NavMeshProjectSettingsObject;
        SerializedProperty m_Areas;

        SerializedProperty m_Agents;
        SerializedProperty m_SettingNames;

        const string kRootPath = "m_BuildSettings.";

        static Styles s_Styles;

        Vector2 m_ScrollPos = Vector2.zero;
        bool m_Advanced;

        ReorderableList m_AreasList;
        ReorderableList m_AgentsList;

        enum Mode
        {
            AgentSettings = 0,
            AreaSettings = 1,
            SceneBakeSettings = 2,
            ObjectSettings = 3
        }

        Mode m_Mode = Mode.ObjectSettings;

        class Styles
        {
            public readonly GUIStyle m_WebLinkLabel;

            // Agents tab
            public readonly GUIContent m_AgentNameContent = EditorGUIUtility.TrTextContent("Name", "The identifier for a set of values of the agent characteristics.");
            public readonly GUIContent m_AgentRadiusContent = EditorGUIUtility.TrTextContent("Radius", "The minimum distance to the walls where the navigation mesh can exist.");
            public readonly GUIContent m_AgentHeightContent = EditorGUIUtility.TrTextContent("Height", "How much vertical clearance space must exist.");
            public readonly GUIContent m_AgentClimbContent = EditorGUIUtility.TrTextContent("Step Height", "The height of discontinuities in the level the agent can climb over (i.e. steps and stairs).");
            public readonly GUIContent m_AgentSlopeContent = EditorGUIUtility.TrTextContent("Max Slope", "Maximum slope the agent can walk up.");

            // Bake tab
            public readonly GUIContent m_BakeRadiusContent = EditorGUIUtility.TrTextContent("Agent Radius", "The minimum distance to the walls where the navigation mesh can exist.");
            public readonly GUIContent m_BakeHeightContent = EditorGUIUtility.TrTextContent("Agent Height", "How much vertical clearance space must exist.");
            public readonly GUIContent m_BakeSlopeContent = EditorGUIUtility.TrTextContent("Max Slope", "Maximum slope the agent can walk up.");
            public readonly GUIContent m_BakeClimbContent = EditorGUIUtility.TrTextContent("Step Height", "The height of discontinuities in the level the agent can climb over (i.e. steps and stairs).");
            public readonly GUIContent m_BakeDropContent = EditorGUIUtility.TrTextContent("Drop Height", "Maximum agent drop height.");
            public readonly GUIContent m_BakeJumpContent = EditorGUIUtility.TrTextContent("Jump Distance", "Maximum agent jump distance.");
            public readonly GUIContent m_BakeManualCellSizeContent = EditorGUIUtility.TrTextContent("Manual Voxel Size", "Enable to set voxel size manually.");
            public readonly GUIContent m_BakeCellSizeContent = EditorGUIUtility.TrTextContent("Voxel Size", "Specifies the voxelization resolution at which the NavMesh is build.");
            public readonly GUIContent m_BakeMinRegionAreaContent = EditorGUIUtility.TrTextContent("Min Region Area", "Minimum area that a navmesh region can be.");
            public readonly GUIContent m_BakePlacementContent = EditorGUIUtility.TrTextContent("Height Mesh", "Generate an accurate height mesh for precise agent placement (slower).");

            public readonly GUIContent m_WindowDeprecationMessage = EditorGUIUtility.TrTextContent("This window will be deprecated.\nGo in the menu Window -> AI -> Navigation in order to open the window provided by the AI Navigation package.");
            public readonly GUIContent m_ComponentWorfklowWebLinklabel = EditorGUIUtility.TrTextContent("For details, visit the Unity manual");

            public readonly GUIContent m_AgentSizeHeader = EditorGUIUtility.TrTextContent("Baked Agent Size");
            public readonly GUIContent m_OffmeshHeader = EditorGUIUtility.TrTextContent("Generated Off Mesh Links");
            public readonly GUIContent m_AdvancedHeader = EditorGUIUtility.TrTextContent("Advanced");
            public readonly GUIContent m_AgentTypesHeader = EditorGUIUtility.TrTextContent("Agent Types");

            public readonly GUIContent m_NameLabel = EditorGUIUtility.TrTextContent("Name");
            public readonly GUIContent m_CostLabel = EditorGUIUtility.TrTextContent("Cost");

            public readonly GUIContent[] m_ModeToggles =
            {
                EditorGUIUtility.TrTextContent("Agents", "Navmesh agent settings."),
                EditorGUIUtility.TrTextContent("Areas", "Navmesh area settings."),
                EditorGUIUtility.TrTextContent("Bake", "Navmesh bake settings."),
                EditorGUIUtility.TrTextContent("Object", "Bake settings for the currently selected object."),
            };

            public Styles()
            {
                m_WebLinkLabel = new GUIStyle(EditorStyles.linkLabel);
                m_WebLinkLabel.fontSize = EditorStyles.miniLabel.fontSize;
                m_WebLinkLabel.wordWrap = true;
            }
        }

        public static void SetupWindow()
        {
            var window = GetWindow<NavMeshEditorWindow>(typeof(InspectorWindow));
            window.minSize = new Vector2(300, 360);
        }

        public static void OpenAreaSettings()
        {
            SetupWindow();
            if (s_NavMeshEditorWindow == null)
                return;

            s_NavMeshEditorWindow.m_Mode = Mode.AreaSettings;
            s_NavMeshEditorWindow.InitProjectSettings();
            s_NavMeshEditorWindow.InitAgents();
        }

        public static void OpenAgentSettings(int agentTypeID)
        {
            SetupWindow();
            if (s_NavMeshEditorWindow == null)
                return;

            s_NavMeshEditorWindow.m_Mode = Mode.AgentSettings;
            s_NavMeshEditorWindow.InitProjectSettings();
            s_NavMeshEditorWindow.InitAgents();

            s_NavMeshEditorWindow.m_AgentsList.index = -1;
            for (int i = 0; i < s_NavMeshEditorWindow.m_Agents.arraySize; i++)
            {
                SerializedProperty agent = s_NavMeshEditorWindow.m_Agents.GetArrayElementAtIndex(i);
                SerializedProperty idProp = agent.FindPropertyRelative("agentTypeID");
                if (idProp.intValue == agentTypeID)
                {
                    s_NavMeshEditorWindow.m_AgentsList.index = i;
                    break;
                }
            }
        }

        public void OnEnable()
        {
            titleContent = GetLocalizedTitleContent();
            s_NavMeshEditorWindow = this;
            EditorApplication.searchChanged += Repaint;

            Repaint();
        }

        void InitProjectSettings()
        {
            if (m_NavMeshProjectSettingsObject == null)
            {
                Object obj = Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings");
                m_NavMeshProjectSettingsObject = new SerializedObject(obj);
            }
        }

        private void InitSceneBakeSettings()
        {
            // Scene based agent bake settings
            m_SettingsObject = new SerializedObject(EditorNavMeshBuilder.navMeshSettingsObject);

            m_AgentRadius = m_SettingsObject.FindProperty(kRootPath + "agentRadius");
            m_AgentHeight = m_SettingsObject.FindProperty(kRootPath + "agentHeight");
            m_AgentSlope = m_SettingsObject.FindProperty(kRootPath + "agentSlope");
            m_LedgeDropHeight = m_SettingsObject.FindProperty(kRootPath + "ledgeDropHeight");
            m_AgentClimb = m_SettingsObject.FindProperty(kRootPath + "agentClimb");
            m_MaxJumpAcrossDistance = m_SettingsObject.FindProperty(kRootPath + "maxJumpAcrossDistance");

            //Advanced Settings
            m_MinRegionArea = m_SettingsObject.FindProperty(kRootPath + "minRegionArea");
            m_ManualCellSize = m_SettingsObject.FindProperty(kRootPath + "manualCellSize");
            m_CellSize = m_SettingsObject.FindProperty(kRootPath + "cellSize");
            m_BuildHeightMesh = m_SettingsObject.FindProperty(kRootPath + "buildHeightMesh");
        }

        private void InitAreas()
        {
            if (m_Areas == null)
            {
                m_Areas = m_NavMeshProjectSettingsObject.FindProperty("areas");
            }
            if (m_AreasList == null)
            {
                m_AreasList = new ReorderableList(m_NavMeshProjectSettingsObject, m_Areas, false, false, false, false);
                m_AreasList.drawElementCallback = DrawAreaListElement;
                m_AreasList.drawHeaderCallback = DrawAreaListHeader;
                m_AreasList.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private void InitAgents()
        {
            if (m_Agents == null)
            {
                m_Agents = m_NavMeshProjectSettingsObject.FindProperty("m_Settings");
                m_SettingNames = m_NavMeshProjectSettingsObject.FindProperty("m_SettingNames");
            }
            if (m_AgentsList == null)
            {
                m_AgentsList = new ReorderableList(m_NavMeshProjectSettingsObject, m_Agents, false, false, true, true);
                m_AgentsList.drawElementCallback = DrawAgentListElement;
                m_AgentsList.drawHeaderCallback = DrawAgentListHeader;
                m_AgentsList.onAddCallback = AddAgent;
                m_AgentsList.onRemoveCallback = RemoveAgent;
                m_AgentsList.elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        // This code is replicated from the navmesh debug draw code.
        private int Bit(int a, int b)
        {
            return (a & (1 << b)) >> b;
        }

        private Color GetAreaColor(int i)
        {
            if (i == 0)
                return new Color(0, 0.75f, 1.0f, 0.5f);
            int r = (Bit(i, 4) + Bit(i, 1) * 2 + 1) * 63;
            int g = (Bit(i, 3) + Bit(i, 2) * 2 + 1) * 63;
            int b = (Bit(i, 5) + Bit(i, 0) * 2 + 1) * 63;
            return new Color((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f, 0.5f);
        }

        public void OnDisable()
        {
            s_NavMeshEditorWindow = null;
            EditorApplication.searchChanged -= Repaint;
        }

        void OnSelectionChange()
        {
            m_ScrollPos = Vector2.zero;
            if (m_Mode == Mode.ObjectSettings)
                Repaint();
        }

        void ModeToggle()
        {
            if (s_Styles == null)
                s_Styles = new Styles();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            m_Mode = (Mode)GUILayout.Toolbar((int)m_Mode, s_Styles.m_ModeToggles, "LargeButton", GUI.ToolbarButtonSize.FitToContents);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void GetAreaListRects(Rect rect, out Rect stripeRect, out Rect labelRect, out Rect nameRect, out Rect costRect)
        {
            float stripeWidth = EditorGUIUtility.singleLineHeight * 0.8f;
            float labelWidth = EditorGUIUtility.singleLineHeight * 5;
            float costWidth = EditorGUIUtility.singleLineHeight * 4;
            float nameWidth = rect.width - stripeWidth - labelWidth - costWidth;
            float x = rect.x;
            stripeRect = new Rect(x, rect.y, stripeWidth - 4, rect.height);
            x += stripeWidth;
            labelRect = new Rect(x, rect.y, labelWidth - 4, rect.height);
            x += labelWidth;
            nameRect = new Rect(x, rect.y, nameWidth - 4, rect.height);
            x += nameWidth;
            costRect = new Rect(x, rect.y, costWidth, rect.height);
        }

        private void DrawAreaListHeader(Rect rect)
        {
            Rect stripeRect, labelRect, nameRect, costRect;
            GetAreaListRects(rect, out stripeRect, out labelRect, out nameRect, out costRect);
            GUI.Label(nameRect, s_Styles.m_NameLabel);
            GUI.Label(costRect, s_Styles.m_CostLabel);
        }

        private void DrawAreaListElement(Rect rect, int index, bool selected, bool focused)
        {
            SerializedProperty areaProp = m_Areas.GetArrayElementAtIndex(index);
            if (areaProp == null)
                return;
            SerializedProperty nameProp = areaProp.FindPropertyRelative("name");
            SerializedProperty costProp = areaProp.FindPropertyRelative("cost");
            if (nameProp == null || costProp == null)
                return;

            rect.height -= 2; // nicer looking with selected list row and a text field in it

            bool builtInLayer = false;
            bool allowChangeName = true;
            bool allowChangeCost = true;
            switch (index)
            {
                case 0: // Default
                    builtInLayer = true;
                    allowChangeName = false;
                    allowChangeCost = true;
                    break;
                case 1: // NonWalkable
                    builtInLayer = true;
                    allowChangeName = false;
                    allowChangeCost = false;
                    break;
                case 2: // Jump
                    builtInLayer = true;
                    allowChangeName = false;
                    allowChangeCost = true;
                    break;
                default:
                    builtInLayer = false;
                    allowChangeName = true;
                    allowChangeCost = true;
                    break;
            }

            Rect stripeRect, labelRect, nameRect, costRect;
            GetAreaListRects(rect, out stripeRect, out labelRect, out nameRect, out costRect);

            bool oldEnabled = GUI.enabled;
            Color color = GetAreaColor(index);
            Color dimmed = new Color(color.r * 0.1f, color.g * 0.1f, color.b * 0.1f, 0.6f);
            EditorGUI.DrawRect(stripeRect, color);

            EditorGUI.DrawRect(new Rect(stripeRect.x, stripeRect.y, 1, stripeRect.height), dimmed);
            EditorGUI.DrawRect(new Rect(stripeRect.x + stripeRect.width - 1, stripeRect.y, 1, stripeRect.height), dimmed);
            EditorGUI.DrawRect(new Rect(stripeRect.x + 1, stripeRect.y, stripeRect.width - 2, 1), dimmed);
            EditorGUI.DrawRect(new Rect(stripeRect.x + 1, stripeRect.y + stripeRect.height - 1, stripeRect.width - 2, 1), dimmed);

            if (builtInLayer)
                GUI.Label(labelRect, EditorGUIUtility.TempContent("Built-in " + index));
            else
                GUI.Label(labelRect, EditorGUIUtility.TempContent("User " + index));

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            GUI.enabled = oldEnabled && allowChangeName;
            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

            GUI.enabled = oldEnabled && allowChangeCost;
            EditorGUI.PropertyField(costRect, costProp, GUIContent.none);

            GUI.enabled = oldEnabled;

            EditorGUI.indentLevel = oldIndent;
        }

        private void AddAgent(ReorderableList list)
        {
            UnityEngine.AI.NavMesh.CreateSettings();
            list.index = UnityEngine.AI.NavMesh.GetSettingsCount() - 1;
        }

        private void RemoveAgent(ReorderableList list)
        {
            SerializedProperty agentProp = m_Agents.GetArrayElementAtIndex(list.index);
            if (agentProp == null)
                return;
            SerializedProperty idProp = agentProp.FindPropertyRelative("agentTypeID");
            if (idProp == null)
                return;
            // Cannot delete default.
            if (idProp.intValue == 0)
                return;

            m_SettingNames.DeleteArrayElementAtIndex(list.index);
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
        }

        private void DrawAgentListHeader(Rect rect)
        {
            GUI.Label(rect, s_Styles.m_AgentTypesHeader);
        }

        private void DrawAgentListElement(Rect rect, int index, bool selected, bool focused)
        {
            SerializedProperty agentProp = m_Agents.GetArrayElementAtIndex(index);
            if (agentProp == null)
                return;
            SerializedProperty idProp = agentProp.FindPropertyRelative("agentTypeID");
            if (idProp == null)
                return;

            rect.height -= 2; // nicer looking with selected list row and a text field in it

            bool isDefault = idProp.intValue == 0;
            using (new EditorGUI.DisabledScope(isDefault))
            {
                var sname = UnityEngine.AI.NavMesh.GetSettingsNameFromID(idProp.intValue);
                GUI.Label(rect, EditorGUIUtility.TempContent(sname));
            }
        }

        public void OnGUI()
        {
            EditorGUILayout.Space();
            ModeToggle();
            EditorGUILayout.Space();

            InitProjectSettings();

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);
            switch (m_Mode)
            {
                case Mode.ObjectSettings:
                    ObjectSettings();
                    break;
                case Mode.SceneBakeSettings:
                    SceneBakeSettings();
                    break;
                case Mode.AreaSettings:
                    AreaSettings();
                    break;
                case Mode.AgentSettings:
                    AgentSettings();
                    break;
            }

            WindowDeprecationWarning();

            EditorGUILayout.EndScrollView();
        }

        public virtual void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(EditorGUIUtility.TrTextContent("Reset Legacy Bake Settings"), false, ResetBakeSettings);
        }

        void ResetBakeSettings()
        {
            Unsupported.SmartReset(EditorNavMeshBuilder.navMeshSettingsObject);
        }

        [RequiredByNativeCode]
        public static void BackgroundTaskStatusChanged()
        {
            if (s_NavMeshEditorWindow != null)
                s_NavMeshEditorWindow.Repaint();
        }

        static IEnumerable<GameObject> GetObjectsRecurse(GameObject root)
        {
            var objects = new List<GameObject> {root};
            foreach (Transform t in root.transform)
                objects.AddRange(GetObjectsRecurse(t.gameObject));
            return objects;
        }

        static List<GameObject> GetObjects(bool includeChildren)
        {
            if (includeChildren)
            {
                var objects = new List<GameObject>();
                foreach (var selected in Selection.gameObjects)
                {
                    objects.AddRange(GetObjectsRecurse(selected));
                }
                return objects;
            }
            return new List<GameObject>(Selection.gameObjects);
        }

        static void SetNavMeshArea(int area, bool includeChildren)
        {
            var objects = GetObjects(includeChildren);
            if (objects.Count <= 0) return;

            Undo.RecordObjects(objects.ToArray(), "Change NavMesh area");
            foreach (var go in objects)
                GameObjectUtility.SetNavMeshArea(go, area);
        }

        private static void WindowDeprecationWarning()
        {
            EditorGUILayout.Space();
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(EditorGUIUtility.GetHelpIcon(MessageType.Warning), GUILayout.ExpandWidth(false));
            GUILayout.BeginVertical();
            GUILayout.Label(s_Styles.m_WindowDeprecationMessage, EditorStyles.wordWrappedMiniLabel);

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(s_Styles.m_ComponentWorfklowWebLinklabel, s_Styles.m_WebLinkLabel))
                Help.BrowseURL($"https://docs.unity3d.com/{Application.unityVersionVer}.{Application.unityVersionMaj}/Documentation/Manual/NavMesh-BuildingComponents.html");
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void ObjectSettings()
        {
            bool emptySelection = true;
            GameObject[] gos;
            SceneModeUtility.SearchBar(typeof(MeshRenderer), typeof(Terrain));
            EditorGUILayout.Space();

            MeshRenderer[] renderers = SceneModeUtility.GetSelectedObjectsOfType<MeshRenderer>(out gos);
            if (gos.Length > 0)
            {
                emptySelection = false;
                ObjectSettings(renderers, gos);
            }

            Terrain[] terrains = SceneModeUtility.GetSelectedObjectsOfType<Terrain>(out gos);
            if (gos.Length > 0)
            {
                emptySelection = false;
                ObjectSettings(terrains, gos);
            }

            if (emptySelection)
                GUILayout.Label("Select a MeshRenderer or a Terrain from the scene.", EditorStyles.helpBox);
        }

        private static void ObjectSettings(Object[] components, GameObject[] gos)
        {
            EditorGUILayout.MultiSelectionObjectTitleBar(components);

            using (var so = new SerializedObject(gos))
            {
                #pragma warning disable 618
                using (new EditorGUI.DisabledScope(!SceneModeUtility.StaticFlagField("Navigation Static", so.FindProperty("m_StaticEditorFlags"), (int)StaticEditorFlags.NavigationStatic)))
                {
                    SceneModeUtility.StaticFlagField("Generate OffMeshLinks", so.FindProperty("m_StaticEditorFlags"), (int)StaticEditorFlags.OffMeshLinkGeneration);
                #pragma warning restore 618

                    var property = so.FindProperty("m_NavMeshLayer");

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                    var navAreaNames = GameObjectUtility.GetNavMeshAreaNames();
                    var currentAbsoluteIndex = GameObjectUtility.GetNavMeshArea(gos[0]);

                    var navAreaIndex = -1;

                    // Need to find the index as the list of names will compress out empty area
                    for (var i = 0; i < navAreaNames.Length; i++)
                    {
                        if (GameObjectUtility.GetNavMeshAreaFromName(navAreaNames[i]) == currentAbsoluteIndex)
                        {
                            navAreaIndex = i;
                            break;
                        }
                    }

                    var navMeshArea = EditorGUILayout.Popup("Navigation Area", navAreaIndex, navAreaNames);
                    EditorGUI.showMixedValue = false;

                    if (EditorGUI.EndChangeCheck())
                    {
                        //Convert the selected index into absolute index...
                        var newNavAreaIndex = GameObjectUtility.GetNavMeshAreaFromName(navAreaNames[navMeshArea]);

                        GameObjectUtility.ShouldIncludeChildren includeChildren = GameObjectUtility.DisplayUpdateChildrenDialogIfNeeded(Selection.gameObjects,
                            "Change Navigation Area", "Do you want change the navigation area to " + navAreaNames[navMeshArea] + " for all the child objects as well?");
                        if (includeChildren != GameObjectUtility.ShouldIncludeChildren.Cancel)
                        {
                            property.intValue = newNavAreaIndex;
                            SetNavMeshArea(newNavAreaIndex, includeChildren == 0);
                        }
                    }
                }

                so.ApplyModifiedProperties();
            }
        }

        private void SceneBakeSettings()
        {
            if (m_SettingsObject == null || m_SettingsObject.targetObject == null)
                InitSceneBakeSettings();

            m_SettingsObject.Update();

            EditorGUILayout.LabelField(s_Styles.m_AgentSizeHeader, EditorStyles.boldLabel);

            // Draw image
            const float kDiagramHeight = 120.0f;
            Rect agentDiagramRect = EditorGUILayout.GetControlRect(false, kDiagramHeight);
            NavMeshEditorHelpers.DrawAgentDiagram(agentDiagramRect, m_AgentRadius.floatValue, m_AgentHeight.floatValue, m_AgentClimb.floatValue, m_AgentSlope.floatValue);

            //Agent Settings
            var radius = EditorGUILayout.FloatField(s_Styles.m_BakeRadiusContent, m_AgentRadius.floatValue);
            if (radius >= 0.001f && !Mathf.Approximately(radius - m_AgentRadius.floatValue, 0.0f))
            {
                m_AgentRadius.floatValue = radius;
                // Update cellsize based on radius unless cellsize is set manually.
                if (m_ManualCellSize.boolValue == false)
                    m_CellSize.floatValue = (2.0f * m_AgentRadius.floatValue) / 6.0f;
            }
            // If radius is really small warn the user about it and instruct common use case for small radius.
            if (m_AgentRadius.floatValue < 0.05f && m_ManualCellSize.boolValue == false)
            {
                EditorGUILayout.HelpBox("The agent radius you've set is really small, this can slow down the build.\nIf you intended to allow the agent to move close to the borders and walls, please adjust voxel size in advaced settings to ensure correct bake.", MessageType.Warning);
            }

            var height = EditorGUILayout.FloatField(s_Styles.m_BakeHeightContent, m_AgentHeight.floatValue);
            if (height >= 0.001f && !Mathf.Approximately(height - m_AgentHeight.floatValue, 0.0f))
                m_AgentHeight.floatValue = height;

            const float kMaxSlopeAngle = 60.0f;
            EditorGUILayout.Slider(m_AgentSlope, 0.0f, kMaxSlopeAngle, s_Styles.m_BakeSlopeContent);
            if (m_AgentSlope.floatValue > kMaxSlopeAngle)
            {
                EditorGUILayout.HelpBox("The maximum slope should be set to less than " + kMaxSlopeAngle + " degrees to prevent NavMesh build artifacts on slopes. ", MessageType.Warning);
            }

            //Step height
            var newClimb = EditorGUILayout.FloatField(s_Styles.m_BakeClimbContent, m_AgentClimb.floatValue);
            if (newClimb >= 0.0f && !Mathf.Approximately(m_AgentClimb.floatValue - newClimb, 0.0f))
                m_AgentClimb.floatValue = newClimb;

            if (m_AgentClimb.floatValue > m_AgentHeight.floatValue)
            {
                // Actual clamping happens in NavMeshBuilder.cpp ConfigureConfig()
                EditorGUILayout.HelpBox("Step height should be less than agent height.\nClamping step height to " + m_AgentHeight.floatValue + " internally when baking.", MessageType.Warning);
            }

            // Detect when agent slope and step height conflict.
            float cs = m_CellSize.floatValue;
            float ch = cs * 0.5f; // From NavMeshBuilder.cpp:ConfigureConfig()
            int walkableClimbVx = (int)Mathf.Ceil(m_AgentClimb.floatValue / ch);

            // Recast treats voxels whose neighbours min/max height difference is more than step height.
            float slopeHeightPerVoxel = Mathf.Tan(m_AgentSlope.floatValue / 180.0f * Mathf.PI) * cs;
            int slopeVx = (int)Mathf.Ceil(slopeHeightPerVoxel * 2.0f / ch);
            if (slopeVx > walkableClimbVx)
            {
                // Recommend new values.
                float betterSlope = (walkableClimbVx * ch) / (cs * 2.0f);
                float betterSlopeAngle = Mathf.Atan(betterSlope) / Mathf.PI * 180.0f;
                float betterStep = (slopeVx - 1) * ch;
                EditorGUILayout.HelpBox("Step Height conflicts with Max Slope. This makes some slopes unwalkable.\nConsider decreasing Max Slope to < " + betterSlopeAngle.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat) + " degrees.\nOr, increase Step Height to > " + betterStep.ToString("0.00") + ".", MessageType.Warning);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField(s_Styles.m_OffmeshHeader, EditorStyles.boldLabel);

            //Drop height
            var newDropHeight =  EditorGUILayout.FloatField(s_Styles.m_BakeDropContent, m_LedgeDropHeight.floatValue);
            if (newDropHeight >= 0.0f && !Mathf.Approximately(newDropHeight - m_LedgeDropHeight.floatValue, 0.0f))
                m_LedgeDropHeight.floatValue = newDropHeight;

            //Jump distance
            var newJumpDistance = EditorGUILayout.FloatField(s_Styles.m_BakeJumpContent, m_MaxJumpAcrossDistance.floatValue);
            if (newJumpDistance >= 0.0f && !Mathf.Approximately(newJumpDistance - m_MaxJumpAcrossDistance.floatValue, 0.0f))
                m_MaxJumpAcrossDistance.floatValue = newJumpDistance;

            EditorGUILayout.Space();

            //Advanced Settings
            m_Advanced = GUILayout.Toggle(m_Advanced, s_Styles.m_AdvancedHeader, EditorStyles.foldout);

            if (m_Advanced)
            {
                EditorGUI.indentLevel++;

                // Cell size
                var manualCellSize = EditorGUILayout.Toggle(s_Styles.m_BakeManualCellSizeContent, m_ManualCellSize.boolValue);
                if (manualCellSize != m_ManualCellSize.boolValue)
                {
                    m_ManualCellSize.boolValue = manualCellSize;
                    // When unchecking the manual control, revert to automatic value.
                    if (!manualCellSize)
                    {
                        m_CellSize.floatValue = (2.0f * m_AgentRadius.floatValue) / 6.0f;
                    }
                }

                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!m_ManualCellSize.boolValue))
                {
                    var cellSize = EditorGUILayout.FloatField(s_Styles.m_BakeCellSizeContent, m_CellSize.floatValue);
                    if (cellSize > 0.0f && !Mathf.Approximately(cellSize - m_CellSize.floatValue, 0.0f))
                    {
                        m_CellSize.floatValue = Math.Max(0.01f, cellSize);
                    }
                    if (cellSize < 0.01f)
                    {
                        EditorGUILayout.HelpBox("The voxel size should be larger than 0.01.", MessageType.Warning);
                    }

                    float voxelsPerRadius = m_CellSize.floatValue > 0 ? (m_AgentRadius.floatValue / m_CellSize.floatValue) : 0.0f;
                    EditorGUILayout.LabelField(" ", voxelsPerRadius.ToString("0.00", CultureInfo.InvariantCulture.NumberFormat) + " voxels per agent radius", EditorStyles.miniLabel);

                    if (m_ManualCellSize.boolValue)
                    {
                        // Make sure these calculations match the ones in NavMeshBuilder.cpp ConfigureConfig()
                        const float kCellSizeToHeightRatio = 0.5f;
                        float cellheight = m_CellSize.floatValue * kCellSizeToHeightRatio;
                        // Some places inside Recast store height as a byte, make sure the ratio between
                        // the agent height and cell height does not exceed this limit.
                        if ((int)Mathf.Floor(m_AgentHeight.floatValue / cellheight) > 250)
                        {
                            float goodValue = (m_AgentHeight.floatValue / 250.0f) / kCellSizeToHeightRatio;
                            EditorGUILayout.HelpBox("The number of voxels per agent height is too high. This will reduce the accuracy of the navmesh. Consider using voxel size of at least " + goodValue.ToString("0.000", CultureInfo.InvariantCulture.NumberFormat) + ".", MessageType.Warning);
                        }

                        if (voxelsPerRadius < 1.0f)
                        {
                            float goodValue = m_AgentRadius.floatValue / 2.0f;
                            EditorGUILayout.HelpBox("The number of voxels per agent radius is too small. The agent may not avoid walls and ledges properly. Consider using a voxel size less than " + goodValue.ToString("0.000", CultureInfo.InvariantCulture.NumberFormat) + " (2 voxels per agent radius).", MessageType.Warning);
                        }
                        else if (voxelsPerRadius > 8.0f)
                        {
                            float goodValue = m_AgentRadius.floatValue / 8.0f;
                            EditorGUILayout.HelpBox("The number of voxels per agent radius is too high. It can cause excessive build times. Consider using voxel size closer to " + goodValue.ToString("0.000", CultureInfo.InvariantCulture.NumberFormat) + " (8 voxels per radius).", MessageType.Warning);
                        }
                    }

                    if (m_ManualCellSize.boolValue)
                    {
                        EditorGUILayout.HelpBox("Voxel size controls how accurately the navigation mesh is generated from the level geometry. A good voxel size is 2-4 voxels per agent radius. Making voxel size smaller will increase build time.", MessageType.None);
                    }
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Min region area
                var minRegionArea = EditorGUILayout.FloatField(s_Styles.m_BakeMinRegionAreaContent, m_MinRegionArea.floatValue);
                if (minRegionArea >= 0.0f && minRegionArea != m_MinRegionArea.floatValue)
                    m_MinRegionArea.floatValue = minRegionArea;

                EditorGUILayout.Space();

                //Height mesh
                var heightMesh = EditorGUILayout.Toggle(s_Styles.m_BakePlacementContent, m_BuildHeightMesh.boolValue);
                if (heightMesh != m_BuildHeightMesh.boolValue)
                    m_BuildHeightMesh.boolValue = heightMesh;

                EditorGUI.indentLevel--;
            }

            m_SettingsObject.ApplyModifiedProperties();
            BakeButtons();
        }

        private void AreaSettings()
        {
            if (m_Areas == null)
                InitAreas();
            m_NavMeshProjectSettingsObject.Update();

            using (new GUILayout.VerticalScope(EditorStyles.defaultContentMargins))
            {
                m_AreasList.DoLayoutList();
            }

            m_NavMeshProjectSettingsObject.ApplyModifiedProperties();
        }

        private void AgentSettings()
        {
            if (m_Agents == null)
                InitAgents();
            m_NavMeshProjectSettingsObject.Update();

            if (m_AgentsList.index < 0)
                m_AgentsList.index = 0;

            using (new GUILayout.VerticalScope(EditorStyles.defaultContentMargins))
            {
                m_AgentsList.DoLayoutList();
            }

            if (m_AgentsList.index >= 0 && m_AgentsList.index < m_Agents.arraySize)
            {
                SerializedProperty nameProp = m_SettingNames.GetArrayElementAtIndex(m_AgentsList.index);
                SerializedProperty selectedAgent = m_Agents.GetArrayElementAtIndex(m_AgentsList.index);

                SerializedProperty radiusProp = selectedAgent.FindPropertyRelative("agentRadius");
                SerializedProperty heightProp = selectedAgent.FindPropertyRelative("agentHeight");
                SerializedProperty stepHeightProp = selectedAgent.FindPropertyRelative("agentClimb");
                SerializedProperty maxSlopeProp = selectedAgent.FindPropertyRelative("agentSlope");

                const float kDiagramHeight = 120.0f;
                Rect agentDiagramRect = EditorGUILayout.GetControlRect(false, kDiagramHeight);
                NavMeshEditorHelpers.DrawAgentDiagram(agentDiagramRect, radiusProp.floatValue, heightProp.floatValue, stepHeightProp.floatValue, maxSlopeProp.floatValue);

                EditorGUILayout.PropertyField(nameProp, s_Styles.m_AgentNameContent);
                EditorGUILayout.PropertyField(radiusProp, s_Styles.m_AgentRadiusContent);
                EditorGUILayout.PropertyField(heightProp, s_Styles.m_AgentHeightContent);
                EditorGUILayout.PropertyField(stepHeightProp, s_Styles.m_AgentClimbContent);

                const float kMaxSlopeAngle = 60.0f;
                EditorGUILayout.Slider(maxSlopeProp, 0.0f, kMaxSlopeAngle, s_Styles.m_AgentSlopeContent);
            }

            EditorGUILayout.Space();

            m_NavMeshProjectSettingsObject.ApplyModifiedProperties();
        }

        static void BakeButtons()
        {
            const float kButtonWidth = 95;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool wasEnabled = GUI.enabled;
            GUI.enabled &= !Application.isPlaying;
            if (GUILayout.Button("Clear", GUILayout.Width(kButtonWidth)))
            {
                EditorNavMeshBuilder.ClearAllNavMeshes();
            }
            GUI.enabled = wasEnabled;

            if (EditorNavMeshBuilder.isRunning)
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(kButtonWidth)))
                    EditorNavMeshBuilder.Cancel();
            }
            else
            {
                wasEnabled = GUI.enabled;
                GUI.enabled &= !Application.isPlaying;
                if (GUILayout.Button("Bake", GUILayout.Width(kButtonWidth)))
                {
                    EditorNavMeshBuilder.BuildNavMeshAsync();
                }
                GUI.enabled = wasEnabled;
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }
    }
}
