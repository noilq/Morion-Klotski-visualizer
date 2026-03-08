using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KlotskiDecisionTree;
using TMPro;
using System.Collections;
using System.Linq;
using UnityEngine.EventSystems;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class DecisionTreeVisualizer : MonoBehaviour
{
    public DecisionTreeUIController ui;

    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private GameObject rootNodePrefab;
    [SerializeField] private GameObject winningNodePrefab;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Material shortestPathLineMaterial;
    [SerializeField] private float repulsionForce = 200f;
    [SerializeField] private float springForce = 50f;
    [SerializeField] private float damping = 0.95f;
    [SerializeField] private float minDistance = 4f;
    [SerializeField] private float maxVelocity = 0.3f;
    [SerializeField] private float velocityThreshold = 0.1f;
    [SerializeField] private float nodeSpawnDelay = 0.01f;

    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private TMP_InputField rowsInput;
    [SerializeField] private TMP_InputField columnsInput;
    [SerializeField] private Toggle pinsToggle;
    [SerializeField] private TMP_InputField winningBlockIdInput;
    [SerializeField] private TMP_InputField winningXInput;
    [SerializeField] private TMP_InputField winningYInput;
    [SerializeField] private TMP_InputField exitWidthInput;
    [SerializeField] private UnityEngine.UI.Button selectWinningTileButton;

    [SerializeField] private RectTransform boardPreviewContainer;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject blockPreviewPrefab;
    private readonly List<GameObject> cellObjects = new List<GameObject>();
    private readonly List<GameObject> blockObjects = new List<GameObject>();
    [SerializeField] private GameObject exitPreviewPrefab;
    private GameObject exitObject;

    [SerializeField] private GameObject pinPrefab;
    private readonly List<GameObject> pinObjects = new List<GameObject>();

    [SerializeField] private GameObject graphSettingPanel;
    [SerializeField] private Button graphSettingsButton;
    [SerializeField] private Slider repulsionForceScrollBar;
    [SerializeField] private Slider springForceScrollBar;
    [SerializeField] private Slider dampingScrollBar;
    [SerializeField] private Slider minDistanceScrollBar;
    [SerializeField] private Slider maxVelocityScrollBar;
    [SerializeField] private Slider velocityTresholdScrollBar;

    public BoardConfig boardConfig = new BoardConfig
    {
        rows = 4,
        columns = 4,
        pinsEnabled = true,
        winningBlockId = 1,
        winningX = 2,
        winningY = 3,
        exitWidth = 2,
        blocks = new List<BlockConfig>
        {
            new BlockConfig { id = 1, width = 2, height = 1, x = 0, y = 0 }
        }
    };

    [System.Serializable]
    public class BoardConfig
    {
        public int rows = 4;
        public int columns = 4;
        public bool pinsEnabled = true;
        public int winningBlockId = -1;
        public int winningX = 0;
        public int winningY = 0;
        public int exitWidth = 1;
        public List<BlockConfig> blocks = new List<BlockConfig>();
    }

    [System.Serializable]
    public class BlockConfig
    {
        public int id;
        public int width = 1;
        public int height = 1;
        public int x = 0;
        public int y = 0;
    }

    private Dictionary<GraphNode, GameObject> nodeObjects = new Dictionary<GraphNode, GameObject>();
    private Dictionary<GraphNode, Vector3> velocities = new Dictionary<GraphNode, Vector3>();
    private List<(GraphNode from, GraphNode to, LineRenderer line)> edges = new List<(GraphNode, GraphNode, LineRenderer)>();
    private bool isStabilized = false;
    private GraphNode selectedNode;

    private bool createBlockMode = false;
    private bool deleteBlockMode = false;
    private Vector2Int? firstBlockPoint = null;
    private bool selectWinningTileMode = false;

    [SerializeField] public Image cursorIcon;
    [SerializeField] public Sprite plusSprite;
    [SerializeField] public Sprite deleteSprite;

    private GameObject selectionHighlight;

    private HashSet<(GraphNode from, GraphNode to)> createdEdges = new HashSet<(GraphNode, GraphNode)>();
    private Dictionary<GraphNode, List<GraphNode>> nodeParents = new Dictionary<GraphNode, List<GraphNode>>();

    private HashSet<(string, string)> GetShortestPathEdges(GraphNode root)
    {
        var pathEdges = new HashSet<(string, string)>();
        var queue = new Queue<GraphNode>();
        var parentMap = new Dictionary<GraphNode, GraphNode>();
        var visited = new HashSet<GraphNode>();

        queue.Enqueue(root);
        visited.Add(root);

        GraphNode winningNode = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            if (current.IsWinning) { winningNode = current; break; }

            foreach (var (child, _) in current.Children)
            {
                if (!visited.Contains(child))
                {
                    visited.Add(child);
                    parentMap[child] = current;
                    queue.Enqueue(child);
                }
            }
        }

        if (winningNode != null)
        {
            GraphNode curr = winningNode;
            while (parentMap.ContainsKey(curr))
            {
                GraphNode parent = parentMap[curr];
                pathEdges.Add((parent.StateHash, curr.StateHash)); 
                curr = parent;
            }
        }
        return pathEdges;
    }

    private void HighlightShortestPath(HashSet<(string, string)> shortestPath)
    {   
        foreach (var edge in edges)
        {   
            if (shortestPath.Contains((edge.from.StateHash, edge.to.StateHash)))
            {   
                edge.line.material = shortestPathLineMaterial;
                edge.line.startWidth = 0.18f; 
                edge.line.endWidth = 0.18f;
                edge.line.sortingOrder = 10;
            }
        }
    }

    private IEnumerator VisualizeDecisionTreeGradually(Board initialBoard)
    {
        DecisionGraphBuilder builder = new DecisionGraphBuilder();
        GraphNode root = builder.BuildGraph(initialBoard);

        HashSet<(string, string)> shortestPath = GetShortestPathEdges(root);

        isStabilized = false;

        var createdNodes = new HashSet<GraphNode>();
        var queuedNodes = new Queue<GraphNode>();

        queuedNodes.Enqueue(root);
        createdNodes.Add(root);

        CreateNodeObject(root);

        while (queuedNodes.Count > 0)
        {
            var node = queuedNodes.Dequeue();

            foreach (var (child, _) in node.Children)
            {   
                if (!createdNodes.Contains(child))
                {
                    createdNodes.Add(child);
                    CreateNodeObject(child, node);
                    queuedNodes.Enqueue(child);
                    yield return new WaitForSeconds(nodeSpawnDelay);
                }

                bool isShortest = shortestPath.Contains((node.StateHash, child.StateHash)); 
                CreateEdge(node, child, isShortest);
            }
        }

        HighlightShortestPath(shortestPath);

        isStabilized = false;
    }

    private Vector3 GetFallbackPosition(GraphNode parent)
    {
        if (parent != null && nodeObjects.TryGetValue(parent, out var parentObj))
        {
            return parentObj.transform.position +
                Random.onUnitSphere * minDistance;
        }

        return Random.insideUnitSphere * 2f;
    }

    private void CreateNodeObject(GraphNode node, GraphNode fallbackParent = null)
    {
        if (nodeObjects.ContainsKey(node))
            return;

        Vector3 spawnPos = CalculateSpawnPosition(node, fallbackParent);

        GameObject prefabToUse;
        if (node.IsStarting && rootNodePrefab != null)
        {
            prefabToUse = rootNodePrefab;
        }
        else if (node.IsWinning && winningNodePrefab != null)
        {
            prefabToUse = winningNodePrefab;
        }
        else
        {
            prefabToUse = nodePrefab;
        }

        GameObject nodeObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);

        //beda
        string prefix = node.IsStarting ? "ROOT_" : (node.IsWinning ? "WIN_" : "");
        nodeObj.name = $"{prefix}Node_{node.StateHash.Substring(0, 8)}";

        nodeObjects[node] = nodeObj;
        velocities[node] = Vector3.zero;
    }

    private Vector3 CalculateSpawnPosition(GraphNode node, GraphNode fallbackParent)
    {
        if (nodeParents.TryGetValue(node, out var parents) && parents.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var p in parents)
            {
                if (nodeObjects.TryGetValue(p, out var pObj))
                {
                    sum += pObj.transform.position;
                    count++;
                }
            }
            if (count > 0) return (sum / count) + Random.insideUnitSphere * minDistance * 0.3f;
        }
        return GetFallbackPosition(fallbackParent);
    }

    private void CreateEdge(GraphNode from, GraphNode to, bool isShortestPath)
    {
        if (createdEdges.Contains((from, to)))
            return;

        createdEdges.Add((from, to));

        if (!nodeParents.TryGetValue(to, out var parents))
        {
            parents = new List<GraphNode>();
            nodeParents[to] = parents;
        }

        if (!parents.Contains(from))
            parents.Add(from);

        if (!nodeObjects.ContainsKey(from) || !nodeObjects.ContainsKey(to))
            return;

        GameObject edgeObj = new GameObject(
            $"Edge_{from.StateHash.Substring(0, 8)}_to_{to.StateHash.Substring(0, 8)}"
        );

        LineRenderer line = edgeObj.AddComponent<LineRenderer>();
        line.material = isShortestPath ? shortestPathLineMaterial : lineMaterial;
        //Debug.Log(isShortestPath);
            
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.positionCount = 2;
        line.startColor = Color.white;
        line.endColor = Color.white;
    
        edges.Add((from, to, line));
    }

    void Start()
    {
        ui.Init(this);

        ui.LoadFromConfig();

        ui.OnGenerateClicked += OnGenerateGraph;
        ui.OnCreateBlockClicked += StartCreateBlockMode;
        ui.OnDeleteBlockClicked += StartDeleteBlockMode;

        //uhhh ummmm
        selectWinningTileButton.onClick.AddListener(StartSelectWinningTileMode);

        SubscribeToGraphSettingsPanelEvents();

        Board initialBoard = CreateBoardFromConfig();
        StartCoroutine(VisualizeDecisionTreeGradually(initialBoard));
        isStabilized = false;
    }

    private void OnGenerateGraph()
    {   
        if (!ValidateBoardConfig(out var errors))
        {
            ErrorPopup.Instance?.Show("Errors:\n" + string.Join("\n", errors));
            return;
        }

        StopAllCoroutines();
        ClearGraph();
        var board = CreateBoardFromConfig();
        StartCoroutine(VisualizeDecisionTreeGradually(board));
        isStabilized = false;
    }

    private void UpdateConfigFromUI()
    {
        try
        {
            if (rowsInput != null)
                boardConfig.rows = int.Parse(rowsInput.text);
            if (columnsInput != null)
                boardConfig.columns = int.Parse(columnsInput.text);
            if (pinsToggle != null)
                boardConfig.pinsEnabled = pinsToggle.isOn;
            if (winningBlockIdInput != null)
                boardConfig.winningBlockId = int.Parse(winningBlockIdInput.text);
            if (winningXInput != null)
                boardConfig.winningX = int.Parse(winningXInput.text);
            if (winningYInput != null)
                boardConfig.winningY = int.Parse(winningYInput.text);
            if (exitWidthInput != null)
                boardConfig.exitWidth = int.Parse(exitWidthInput.text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"invalid input in ui, {e.Message}");
        }
    }
    /*
    private void GenerateGraphFromUI()
    {
        UpdateConfigFromUI();
        UpdateBoardPreview();

        if (!ValidateBoardConfig(out var errors))
        {
            string msg = "Board configuration errors:\n" + string.Join("\n", errors);

            if (ErrorPopup.Instance != null)
                ErrorPopup.Instance.Show(msg);

            Debug.LogError(msg);
            return;
        }

        ClearGraph();
        Board initialBoard = CreateBoardFromConfig();
        VisualizeDecisionTree(initialBoard);
        isStabilized = false;
    }
    */
    private void ClearGraph()
    {
        foreach (var nodeObj in nodeObjects.Values)
        {
            if (nodeObj != null) Destroy(nodeObj);
        }
        foreach (var (_, _, line) in edges)
        {
            if (line != null && line.gameObject != null)
                Destroy(line.gameObject);
        }
        nodeObjects.Clear();
        velocities.Clear();
        edges.Clear();
        nodeParents.Clear();    //nado
        createdEdges.Clear();   //nado
    }

    private Board CreateBoardFromConfig()
    {
        Board board = new Board(boardConfig.rows, boardConfig.columns, boardConfig.pinsEnabled)
        {
            WinningBlockId = boardConfig.winningBlockId,
            WinningX = boardConfig.winningX,
            WinningY = boardConfig.winningY,
            ExitWidth = boardConfig.exitWidth
        };

        foreach (var blockConfig in boardConfig.blocks)
        {
            board.AddBlock(new Block(blockConfig.id, blockConfig.width, blockConfig.height, blockConfig.x, blockConfig.y));
        }

        return board;
    }
    /*
    public void VisualizeDecisionTree(Board initialBoard)
    {
        DecisionGraphBuilder builder = new DecisionGraphBuilder();
        GraphNode root = builder.BuildGraph(initialBoard);
        InitializeNodes(root);
        CreateEdges(root);
    }*/

    private void InitializeNodes(GraphNode root)
    {
        var visited = new HashSet<GraphNode>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (visited.Contains(node)) continue;
            visited.Add(node);

            GameObject prefabToUse;
            if (node.IsStarting && rootNodePrefab != null) prefabToUse = rootNodePrefab;
            else if (node.IsWinning && winningNodePrefab != null) prefabToUse = winningNodePrefab;
            else prefabToUse = nodePrefab;

            GameObject nodeObj = Instantiate(prefabToUse, Random.insideUnitSphere * 10f, Quaternion.identity);

            //nodeObj.name = $"Node_{node.StateHash.Substring(0, 8)}";
            string prefix = node.IsStarting ? "ROOT_" : (node.IsWinning ? "WIN_" : "");
            nodeObj.name = $"{prefix}Node_{node.StateHash.Substring(0, 8)}";
            
            nodeObjects[node] = nodeObj;
            velocities[node] = Vector3.zero;

            foreach (var (child, _) in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }
    /*
    private void CreateEdges(GraphNode root)
    {
        var visited = new HashSet<GraphNode>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (visited.Contains(node)) continue;
            visited.Add(node);

            foreach (var (child, moveDesc) in node.Children)
            {
                if (!nodeObjects.ContainsKey(node) || !nodeObjects.ContainsKey(child)) continue;

                GameObject edgeObj = new GameObject($"Edge_{node.StateHash.Substring(0, 8)}_to_{child.StateHash.Substring(0, 8)}");
                LineRenderer line = edgeObj.AddComponent<LineRenderer>();
                line.material = lineMaterial;
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.positionCount = 2;
                line.startColor = Color.white;
                line.endColor = Color.white;
                edges.Add((node, child, line));

                queue.Enqueue(child);
            }
        }
    }*/

    private void UpdateEdges()
    {
        foreach (var (from, to, line) in edges)
        {
            line.SetPosition(0, nodeObjects[from].transform.position);
            line.SetPosition(1, nodeObjects[to].transform.position);
        }
    }

    private void OnValidate()
    {
        isStabilized = false;
    }

    private void EmergencyClearGraph()
    {
        StopAllCoroutines();
        ClearGraph();
        isStabilized = false;
        selectedNode = null;
    }

    private void Update()
    {      
        if (Input.GetKeyDown(KeyCode.F))
        {
            EmergencyClearGraph();
            return;
        }

        UpdateCursorIcon();
        HandleNodeClick();
        HandleBlockEditingClicks();
        
        UpdateCreationHighlight();
        if (isStabilized)
        {
            UpdateEdges();
            return;
        }

        var forces = new Dictionary<GraphNode, Vector3>();
        foreach (var node in nodeObjects.Keys)
        {
            forces[node] = Vector3.zero;
        }

        var nodes = new List<GraphNode>(nodeObjects.Keys);
        for (int j = 0; j < nodes.Count; j++)
        {
            for (int k = j + 1; k < nodes.Count; k++)
            {
                var node1 = nodes[j];
                var node2 = nodes[k];
                Vector3 pos1 = nodeObjects[node1].transform.position;
                Vector3 pos2 = nodeObjects[node2].transform.position;
                Vector3 delta = pos1 - pos2;
                float distance = delta.magnitude;
                if (distance < 0.01f) distance = 0.01f;
                if (distance < minDistance)
                {
                    float forceMagnitude = repulsionForce * (1f / distance);
                    Vector3 force = delta.normalized * forceMagnitude;
                    forces[node1] += force;
                    forces[node2] -= force;
                }
            }
        }

        foreach (var (from, to, _) in edges)
        {
            Vector3 pos1 = nodeObjects[from].transform.position;
            Vector3 pos2 = nodeObjects[to].transform.position;
            Vector3 delta = pos2 - pos1;
            float distance = delta.magnitude;
            if (distance > 0.01f)
            {
                Vector3 force = delta * springForce;
                forces[from] += force;
                forces[to] -= force;
            }
        }

        foreach (var node in nodeObjects.Keys)
        {
            Vector3 force = forces[node];
            velocities[node] += force * Time.deltaTime;
            velocities[node] *= damping;
            velocities[node] = Vector3.ClampMagnitude(velocities[node], maxVelocity);
            nodeObjects[node].transform.position += velocities[node] * Time.deltaTime;
        }

        float totalVelocity = 0f;
        foreach (var vel in velocities.Values)
        {
            totalVelocity += vel.magnitude;
        }
        if (totalVelocity < velocityThreshold * nodeObjects.Count)
        {
            isStabilized = true;
        }

        UpdateEdges();

        
    }

    private void OnDestroy()
    {
        ClearGraph();
    }

    public void UpdateBoardPreview()
    {
        if (boardPreviewContainer == null) return;
            
        foreach (var go in cellObjects) if(go != null) Destroy(go);
        foreach (var go in blockObjects) if(go != null) Destroy(go);
        if (exitObject != null) Destroy(exitObject);
        cellObjects.Clear();
        blockObjects.Clear();
        if (selectionHighlight != null) selectionHighlight.SetActive(false);
        foreach (var go in pinObjects) Destroy(go);
        pinObjects.Clear();

        int rows = boardConfig.rows;
        int cols = boardConfig.columns;

        if (rows <= 0 || cols <= 0) return;

        float width = boardPreviewContainer.rect.width;
        float height = boardPreviewContainer.rect.height;

        float cellSize = Mathf.Min(width / cols, height / rows);

        float gridWidth = cellSize * cols;
        float gridHeight = cellSize * rows;

        float offsetX = (width - gridWidth) * 0.5f;
        float offsetY = (height - gridHeight) * 0.5f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cell = Instantiate(cellPrefab, boardPreviewContainer);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;

                rect.sizeDelta = new Vector2(cellSize, cellSize);
                rect.anchoredPosition = new Vector2(
                    offsetX + x * cellSize,
                    offsetY + y * cellSize
                );

                var img = cell.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    img.color = (x + y) % 2 == 0 ? 
                        new Color(0.51f, 0.46f, 0.48f) : 
                        new Color(0.38f, 0.34f, 0.36f);

                cellObjects.Add(cell);
            }
        }

        if (boardConfig.pinsEnabled)
        {
            CreatePins(
                rows,
                cols,
                cellSize,
                offsetX,
                offsetY
            );
        }

        Color[] blockColors =
        {
            new Color(0.9f, 0.3f, 0.3f),    //red
            new Color(0.3f, 0.6f, 0.9f),    //blue
            new Color(0.4f, 0.9f, 0.4f),    //green
            new Color(0.9f, 0.8f, 0.4f),    //yellow
            new Color(0.8f, 0.4f, 0.9f),    //violet
            new Color(0.9f, 0.6f, 0.3f),    //orange
            new Color(0.4f, 0.8f, 0.8f),    //light blue
            new Color(0.7f, 0.7f, 0.7f)     //grey
        };

        foreach (var block in boardConfig.blocks)
        {
            GameObject blockGO = Instantiate(blockPreviewPrefab, boardPreviewContainer);
            var rect = blockGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;

            rect.sizeDelta = new Vector2(block.width * cellSize, block.height * cellSize);
            rect.anchoredPosition = new Vector2(offsetX + block.x * cellSize, offsetY + block.y * cellSize);

            var img = blockGO.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                //img.color = block.id == boardConfig.winningBlockId ? new Color(1f, 0.8f, 0.2f) : new Color(0.6f, 0.2f, 0.2f);
                if (block.id == boardConfig.winningBlockId)
                {
                    img.color = new Color(1f, 0.95f, 0.3f);
                }
                else
                {
                    int colorIndex = block.id % blockColors.Length;
                    img.color = blockColors[colorIndex];
                }
            }

            blockObjects.Add(blockGO);
        }

        CreateWinningAreaPreview(cellSize, offsetX, offsetY);
    }

    private void CreateWinningAreaPreview(float cellSize, float offsetX, float offsetY)
    {
        if (exitPreviewPrefab == null) return;
        
        exitObject = Instantiate(exitPreviewPrefab, boardPreviewContainer);
        var rect = exitObject.GetComponent<RectTransform>();
        
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;

        rect.sizeDelta = new Vector2(cellSize, cellSize);
        
        rect.anchoredPosition = new Vector2(
            offsetX + boardConfig.winningX * cellSize, 
            offsetY + boardConfig.winningY * cellSize
        );

        exitObject.name = "WinningExitMarker";

        rect.SetAsLastSibling();
    }

    private void CreatePins(int rows, int cols, float cellSize, float offsetX, float offsetY)
    {
        for (int y = 0; y < rows - 1; y++)
        {
            for (int x = 0; x < cols - 1; x++)
            {
                GameObject pin = Instantiate(pinPrefab, boardPreviewContainer);
                RectTransform rect = pin.GetComponent<RectTransform>();

                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);

                rect.sizeDelta = rect.sizeDelta;

                float px = offsetX + (x + 1) * cellSize;
                float py = offsetY + (y + 1) * cellSize;

                rect.anchoredPosition = new Vector2(px, py);

                pinObjects.Add(pin);
            }
        }
    }


    private void HandleNodeClick()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {   
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var clickedObj = hit.collider.gameObject;
                var nodeEntry = nodeObjects.FirstOrDefault(kvp => kvp.Value == clickedObj);
                if (nodeEntry.Key != null)
                {
                    selectedNode = nodeEntry.Key;
                    ui.ShowNodePreview(selectedNode);
                }
            }
        }
    }

    private void ToggleGraphSettingsPanel()
    {
        if (graphSettingPanel == null)
            return;

        /*bool newState = !graphSettingPanel.activeSelf;
        graphSettingPanel.SetActive(newState);
        */
        if (true)
        {
            repulsionForceScrollBar.value = Mathf.InverseLerp(0f, 500f, repulsionForce);
            springForceScrollBar.value = Mathf.InverseLerp(0f, 200f, springForce);
            dampingScrollBar.value = Mathf.InverseLerp(0.1f, 1f, damping);
            minDistanceScrollBar.value = Mathf.InverseLerp(1f, 10f, minDistance);
            maxVelocityScrollBar.value = Mathf.InverseLerp(0.1f, 10f, maxVelocity);
            velocityTresholdScrollBar.value = Mathf.InverseLerp(0.01f, 1f, velocityThreshold);
        }
    }

    private void SubscribeToGraphSettingsPanelEvents()
    {   
        if (graphSettingsButton != null)
            graphSettingsButton.onClick.AddListener(ToggleGraphSettingsPanel);
            
        if (repulsionForceScrollBar != null)
            repulsionForceScrollBar.onValueChanged.AddListener(OnRepulsionForceChanged);

        if (springForceScrollBar != null)
            springForceScrollBar.onValueChanged.AddListener(OnSpringForceChanged);

        if (dampingScrollBar != null)
            dampingScrollBar.onValueChanged.AddListener(OnDampingChanged);

        if (minDistanceScrollBar != null)
            minDistanceScrollBar.onValueChanged.AddListener(OnMinDistanceChanged);

        if (maxVelocityScrollBar != null)
            maxVelocityScrollBar.onValueChanged.AddListener(OnMaxVelocityChanged);

        if (velocityTresholdScrollBar != null)
            velocityTresholdScrollBar.onValueChanged.AddListener(OnVelocityThresholdChanged);
    }

    private float ReMap(float value, float minIn, float maxIn, float minOut, float maxOut)
    {
        return minOut + (value - minIn) * (maxOut - minOut) / (maxIn - minIn);
    }

    private void OnRepulsionForceChanged(float value)
    {
        repulsionForce = ReMap(value, 0f, 1f, 0f, 500f);
        isStabilized = false;
    }

    private void OnSpringForceChanged(float value)
    {
        springForce = ReMap(value, 0f, 1f, 0f, 200f);
        isStabilized = false;
    }

    private void OnDampingChanged(float value)
    {
        damping = ReMap(value, 0f, 1f, 0f, 1f);
        isStabilized = false;
    }

    private void OnMinDistanceChanged(float value)
    {
        minDistance = ReMap(value, 0f, 1f, 1f, 10f);
        isStabilized = false;
    }

    private void OnMaxVelocityChanged(float value)
    {
        maxVelocity = ReMap(value, 0f, 1f, 0.1f, 10f);
        isStabilized = false;
    }

    private void OnVelocityThresholdChanged(float value)
    {
        velocityThreshold = ReMap(value, 0f, 1f, 0.01f, 1f);
        isStabilized = false;
    }

    private bool ValidateBoardConfig(out List<string> errors)
    {
        errors = new List<string>();

        int rows = boardConfig.rows;
        int cols = boardConfig.columns;

        if (boardConfig.blocks == null || boardConfig.blocks.Count < 2)
        {
            errors.Add("Board must contain at least 2 blocks.");
        }

        foreach (var block in boardConfig.blocks)
        {
            if (block.width < 1 || block.height < 1)
                errors.Add($"Block {block.id} has invalid size ({block.width}x{block.height}).");

            if (block.x < 0 || block.y < 0)
                errors.Add($"Block {block.id} has negative position ({block.x}, {block.y}).");

            if (block.x + block.width > cols || block.y + block.height > rows)
                errors.Add($"Block {block.id} does not fit inside the board (pos {block.x},{block.y}, size {block.width}x{block.height}).");
        }

        for (int i = 0; i < boardConfig.blocks.Count; i++)
        {
            for (int j = i + 1; j < boardConfig.blocks.Count; j++)
            {
                var a = boardConfig.blocks[i];
                var b = boardConfig.blocks[j];

                bool overlap =
                    a.x < b.x + b.width &&
                    a.x + a.width > b.x &&
                    a.y < b.y + b.height &&
                    a.y + a.height > b.y;

                if (overlap)
                    errors.Add($"Blocks {a.id} and {b.id} overlap!");
            }
        }

        if (boardConfig.winningBlockId != -1)
        {
            var winning = boardConfig.blocks.FirstOrDefault(b => b.id == boardConfig.winningBlockId);
            if (winning == null)
            {
                errors.Add($"Winning block ID {boardConfig.winningBlockId} does not exist.");
            }
            else
            {
                if (boardConfig.winningX < 0 ||
                    boardConfig.winningY < 0 ||
                    boardConfig.winningX + boardConfig.exitWidth > cols)
                {
                    errors.Add("Winning exit position is outside the board.");
                }
            }
        }

        return errors.Count == 0;
    }



    private void StartCreateBlockMode()
    {
        createBlockMode = true;
        deleteBlockMode = false;
        firstBlockPoint = null;
    }

    private void StartDeleteBlockMode()
    {
        deleteBlockMode = true;
        createBlockMode = false;
    }

    private void HandleBlockEditingClicks()
    {
        if (!createBlockMode && !deleteBlockMode && !selectWinningTileMode)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        Vector2 localPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardPreviewContainer,
                Input.mousePosition,
                uiCanvas.worldCamera,
                out localPos))
            return;

        float width = boardPreviewContainer.rect.width;
        float height = boardPreviewContainer.rect.height;

        int cols = boardConfig.columns;
        int rows = boardConfig.rows;

        float cellSize = Mathf.Min(width / cols, height / rows);

        float gridWidth = cellSize * cols;
        float gridHeight = cellSize * rows;

        float offsetX = (width - gridWidth) * 0.5f;
        float offsetY = (height - gridHeight) * 0.5f;

        float adjustedX = localPos.x + width * 0.5f - offsetX;
        float adjustedY = localPos.y + height * 0.5f - offsetY;

        int x = Mathf.FloorToInt(adjustedX / cellSize);
        int y = Mathf.FloorToInt(adjustedY / cellSize);

        if (x < 0 || y < 0 || x >= cols || y >= rows)
            return;

        if (deleteBlockMode)
        {
            var block = boardConfig.blocks.FirstOrDefault(b =>
                x >= b.x && x < b.x + b.width &&
                y >= b.y && y < b.y + b.height);

            if (block != null)
            {
                boardConfig.blocks.Remove(block);
                UpdateBoardPreview();
            }

            deleteBlockMode = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        if (createBlockMode)
        {
            if (firstBlockPoint == null)
            {
                firstBlockPoint = new Vector2Int(x, y);
                return;
            }

            Vector2Int p1 = firstBlockPoint.Value;
            Vector2Int p2 = new Vector2Int(x, y);

            int minX = Mathf.Min(p1.x, p2.x);
            int minY = Mathf.Min(p1.y, p2.y);
            int widthB = Mathf.Abs(p1.x - p2.x) + 1;
            int heightB = Mathf.Abs(p1.y - p2.y) + 1;

            foreach (var b in boardConfig.blocks)
            {
                bool overlap =
                    minX < b.x + b.width &&
                    minX + widthB > b.x &&
                    minY < b.y + b.height &&
                    minY + heightB > b.y;

                if (overlap)
                {
                    Debug.LogWarning("Block overlaps existing block!");
                    firstBlockPoint = null;
                    createBlockMode = false;
                    return;
                }
            }

            int newId = GetNextAvailableBlockId();

            boardConfig.blocks.Add(new BlockConfig
            {
                id = newId,
                x = minX,
                y = minY,
                width = widthB,
                height = heightB
            });

            UpdateBoardPreview();

            firstBlockPoint = null;
            createBlockMode = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        if (selectWinningTileMode)
        {
            boardConfig.winningX = x;
            boardConfig.winningY = y;
            Debug.Log($"{x}, {y}");
            UpdateBoardPreview();
            
            selectWinningTileMode = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
    }

    private int GetNextAvailableBlockId()
    {
        var usedIds = new HashSet<int>(boardConfig.blocks.Select(b => b.id));

        int id = 1;
        while (usedIds.Contains(id))
            id++;

        return id;
    }

    [SerializeField] private GameObject cursorTextPrefab;
    [SerializeField] private string createFirstKey = "cursor_mode_create_first";
    [SerializeField] private string createSecondKey = "cursor_mode_create_second";
    [SerializeField] private string deleteKey = "cursor_mode_delete";
    [SerializeField] private string selectKey = "cursor_mode_select_win";

    private GameObject _spawnedCursorText;
    private TextMeshProUGUI _cursorTextMesh;
    private LocalizedText _localizedScript;

    private void UpdateCursorIcon()
    {

        if (!createBlockMode && !deleteBlockMode && !selectWinningTileMode)
        {
            if (_spawnedCursorText != null) _spawnedCursorText.SetActive(false);
            return;
        }

        if (_spawnedCursorText == null)
        {
            _spawnedCursorText = Instantiate(cursorTextPrefab, uiCanvas.transform);
            _cursorTextMesh = _spawnedCursorText.GetComponent<TextMeshProUGUI>();
            _localizedScript = _spawnedCursorText.GetComponent<LocalizedText>();
            _cursorTextMesh.raycastTarget = false;
            _spawnedCursorText.transform.localScale = Vector3.one;
        }

        _spawnedCursorText.SetActive(true);

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)uiCanvas.transform,
            Input.mousePosition,
            uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCanvas.worldCamera,
            out pos
        );

        Vector2 cursorOffset = new Vector2(1080f, -20f); 
        ((RectTransform)_spawnedCursorText.transform).anchoredPosition = pos + cursorOffset;

        string targetKey = "";

        if (createBlockMode)
        {
            targetKey = (firstBlockPoint == null) ? createFirstKey : createSecondKey;
        }
        else if (deleteBlockMode)
        {
            targetKey = deleteKey;
        }
        else if (selectWinningTileMode)
        {
            targetKey = selectKey;
        }

        if (_localizedScript != null && _localizedScript.localizationKey != targetKey)
        {
            _localizedScript.SetKey(targetKey);
        }
    }

    private void StartSelectWinningTileMode()
    {
        selectWinningTileMode = true;
        createBlockMode = false;
        deleteBlockMode = false;
        firstBlockPoint = null;
    }

    private void UpdateCreationHighlight()
    {
        if (!createBlockMode)
        {
            if (selectionHighlight != null && selectionHighlight.activeSelf) 
                selectionHighlight.SetActive(false);
            return;
        }

        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                boardPreviewContainer, Input.mousePosition, uiCanvas.worldCamera, out localPos))
        {
            float width = boardPreviewContainer.rect.width;
            float height = boardPreviewContainer.rect.height;
            int cols = boardConfig.columns;
            int rows = boardConfig.rows;
            float cellSize = Mathf.Min(width / cols, height / rows);
            float offsetX = (width - cellSize * cols) * 0.5f;
            float offsetY = (height - cellSize * rows) * 0.5f;
            float adjustedX = localPos.x + width * 0.5f - offsetX;
            float adjustedY = localPos.y + height * 0.5f - offsetY;
            int curX = Mathf.FloorToInt(adjustedX / cellSize);
            int curY = Mathf.FloorToInt(adjustedY / cellSize);

            if (curX < 0 || curX >= cols || curY < 0 || curY >= rows)
            {
                if (selectionHighlight != null) selectionHighlight.SetActive(false);
                return;
            }

            int startX, startY, w, h;
            if (firstBlockPoint.HasValue)
            {
                startX = Mathf.Min(firstBlockPoint.Value.x, curX);
                startY = Mathf.Min(firstBlockPoint.Value.y, curY);
                w = Mathf.Abs(firstBlockPoint.Value.x - curX) + 1;
                h = Mathf.Abs(firstBlockPoint.Value.y - curY) + 1;
            }
            else
            {
                startX = curX;
                startY = curY;
                w = 1;
                h = 1;
            }

            bool isOverlap = false;
            foreach (var b in boardConfig.blocks)
            {
                if (startX < b.x + b.width && startX + w > b.x &&
                    startY < b.y + b.height && startY + h > b.y)
                {
                    isOverlap = true;
                    break;
                }
            }

            ShowHighlight(startX, startY, w, h, cellSize, offsetX, offsetY, isOverlap);
        }
    }

    private bool CheckOverlap(int x, int y, int w, int h)
    {
        foreach (var b in boardConfig.blocks)
        {
            if (x < b.x + b.width && x + w > b.x &&
                y < b.y + b.height && y + h > b.y) return true;
        }
        return false;
    }

    private void ShowHighlight(int x, int y, int w, int h, float cellSize, float offX, float offY, bool overlap)
    {
        if (selectionHighlight == null)
        {
            selectionHighlight = Instantiate(blockPreviewPrefab, boardPreviewContainer);

            var img = selectionHighlight.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0.4f); 
            selectionHighlight.name = "SelectionHighlight";
        }

        selectionHighlight.SetActive(true);
        selectionHighlight.transform.SetAsLastSibling();
        var rect = selectionHighlight.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(w * cellSize, h * cellSize);
        rect.anchoredPosition = new Vector2(offX + x * cellSize, offY + y * cellSize);

        var image = selectionHighlight.GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.color = overlap ? new Color(1f, 0f, 0f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);
        }
    }
}