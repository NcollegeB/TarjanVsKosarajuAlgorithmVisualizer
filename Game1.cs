using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

// Nathan Bryant
// 411 Final Implementaion of Kosaraju's and Tarjan's Algorithms (despite its name being dijkstraVsKosaraju lol)
// This is a simple visualization using 5 graphs, Microsoft's MonoGame framework, and C#
// program visualizes the two algorithms for finding strongly connected components (SCCs) in directed graphs.
// user can toggle between the two algorithms and switch between different graphs using keyboard inputs.
// program uses a simple graphical interface to display the graphs, nodes, edges, and algorithm execution time.
// graphs are represented as adjacency lists, and the nodes are drawn as circles with colored outlines based on their SCC membership.
// Works much better than the original version I made in rust's bevy game engine.

namespace DijkstraVsKosaraju
{
    public class Game1 : Game
    {
        // --- Algorithm settings and control variables ---
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private bool _useTarjan = false;             // bool algorithm selection (false = Kosaraju's, true = Tarjan's)
        private int _currentGraphIndex = 0;
        private const int _totalGraphs = 5;
        private double _lastAlgorithmTimeMs = 0.0;
        private KeyboardState _prevKeyboardState;

        // Tarjan-specific per-node values
        private int[] _tarjanDisc;
        private int[] _tarjanLow;

        // --- Graph data and animation states section ---
        private List<int>[] _graphAdj;              // Adjacency list for the current graph
        private Vector2[] _nodePositions;           // Positions of nodes in the current graph
        private List<int>[][] _allGraphsAdj;        //  adjacency lists for our graphs
        private Vector2[][] _allGraphsPositions;    //  node positions for our graphs
        private List<List<int>> _stronglyConnectedComponents;  // Strongly connected components of our curr graph
        private Color[] _nodeColors;                // Outline color for each node (based on its SCC)
        private readonly Color[] _sccColors = new Color[] {     // different outline colors that we can have for each SCC
           Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Orange,
           Color.Purple, Color.Cyan, Color.Magenta, Color.Brown, Color.White
       };


        // --- Graphics and drawing assets section ---
        private const int NodeRadius = 10;          // Radius of the nodes (in pixels)
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private Texture2D _circleTexture;


        public Game1()
        {
            // Initialize the graphics device manager and basic settings
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }


        // --- Drawing and Rendering Helper Methods ---
        // Draws a line between two points with the given color using the pixel texture.
        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            // Calculate the distance and angle between the points
            float distance = Vector2.Distance(start, end);
            float angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
            // Draw a stretched pixel between start and end points
            _spriteBatch.Draw(_pixelTexture, start, null, color, angle, Vector2.Zero, new Vector2(distance, 1f), SpriteEffects.None, 0f);
        }


        // Draws a directed edge as an arrow from the start position to the end position.
        private void DrawArrow(Vector2 start, Vector2 end)
        {
            // Draw the main line of our directed edge
            DrawLine(start, end, Color.White);


            // Calculate arrowhead directions with a fixed offset angle 
            float mainAngle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
            float arrowAngle = MathHelper.ToRadians(30f);
            float arrowLength = NodeRadius * 0.5f;       // change for arrow length
                                                         // Calculate the two lines for the arrowhead
            float angle1 = mainAngle + MathF.PI - arrowAngle;
            float angle2 = mainAngle + MathF.PI + arrowAngle;
            // Compute unit direction and offset arrow tip further behind node border
            Vector2 dirNorm = Vector2.Normalize(end - start);
            Vector2 arrowTip = end - dirNorm * (NodeRadius + 4f);  // Tip Offset from the node border
                                                                   // Compute end points of the two arrowhead lines
            Vector2 arrowEnd1 = new Vector2(
                arrowTip.X + arrowLength * (float)Math.Cos(angle1),
                arrowTip.Y + arrowLength * (float)Math.Sin(angle1)
            );
            Vector2 arrowEnd2 = new Vector2(
                arrowTip.X + arrowLength * (float)Math.Cos(angle2),
                arrowTip.Y + arrowLength * (float)Math.Sin(angle2)
            );
            // Draw the two lines of the arrowhead in yellow
            DrawLine(arrowTip, arrowEnd1, Color.Yellow);
            DrawLine(arrowTip, arrowEnd2, Color.Yellow);
        }



        // Function responsible for  drawing a node (circle) at the specified index with the speciofic outline color.
        // The node is drawn as a colored outline with a filled white circle.
        private void DrawNode(int index, Color outlineColor)
        {
            Vector2 position = _nodePositions[index];
            // Draw the colored outline (slightly scalled up)
            _spriteBatch.Draw(_circleTexture, position, null, outlineColor, 0f, new Vector2(NodeRadius, NodeRadius), 1.2f, SpriteEffects.None, 0f);
            // Draw the white filled circle on top
            _spriteBatch.Draw(_circleTexture, position, null, Color.White, 0f, new Vector2(NodeRadius, NodeRadius), 1.0f, SpriteEffects.None, 0f);
            // Draw the node index label centered in the node
            string label = index.ToString();
            Vector2 textSize = _font.MeasureString(label);
            Vector2 textPos = position - textSize * 0.5f;
            _spriteBatch.DrawString(_font, label, textPos, Color.Black);
        }


        // --- Algorithm Logic Methods ---
        // Computes strongly connected components using Tarjan's algorithm and stores the result in _stronglyConnectedComponents
        private void ComputeTarjanSCCs()
        {
            int n = _graphAdj.Length;
            _stronglyConnectedComponents = new List<List<int>>();
            int[] disc = new int[n];
            int[] low = new int[n];
            bool[] inStack = new bool[n];
            Stack<int> stack = new Stack<int>();
            int time = 0;

            // Initialize discovery times and low-link values
            // Init discovery times for all nodes are unvisited (-1)
            for (int i = 0; i < n; i++)
            {
                disc[i] = -1;
                low[i] = 0;
                inStack[i] = false;
            }


            // DFS for Tarjan's algorithm
            void TarjanDFS(int u)
            {
                disc[u] = low[u] = time++;
                stack.Push(u);
                inStack[u] = true;


                // Visit all neighbors of u
                foreach (int v in _graphAdj[u])
                {
                    if (disc[v] == -1)
                    {
                        // Neighbor v not visited: recurse and update low-link value
                        TarjanDFS(v);
                        low[u] = Math.Min(low[u], low[v]);
                    }
                    else if (inStack[v])
                    {
                        // Neighbor v is in stack (part of current SCC path): update low-link value
                        low[u] = Math.Min(low[u], disc[v]);
                    }
                }


                // If u is root of a SCC (no link to an earlier node)
                if (low[u] == disc[u])
                {
                    List<int> component = new List<int>();
                    int w;
                    // Pop stack until u is reached, to get all members of this SCC
                    do
                    {
                        w = stack.Pop();
                        inStack[w] = false;
                        component.Add(w);
                    }
                    while (w != u);
                    _stronglyConnectedComponents.Add(component);
                }
            }


            // Run DFS, each node that has not been visited yet
            for (int i = 0; i < n; i++)
            {
                if (disc[i] == -1)
                {
                    TarjanDFS(i);
                }
            }
            // Store disc and low arrays for drawing
            _tarjanDisc = disc;
            _tarjanLow = low;
        }



        // Computes SCC using Kosaraju's algorithm and stores the result in _stronglyConnectedComponents.
        private void ComputeKosarajuSCCs()
        {
            int n = _graphAdj.Length;
            _stronglyConnectedComponents = new List<List<int>>();
            bool[] visited = new bool[n];
            Stack<int> stack = new Stack<int>();


            // DFS to fill vertices in stack according to their finish times (first pass)
            void FillOrder(int u)
            {
                visited[u] = true;
                foreach (int v in _graphAdj[u])
                {
                    if (!visited[v])
                    {
                        FillOrder(v);
                    }
                }
                stack.Push(u);
            }


            // Perform first pass DFS for all nodes to get finish time order
            for (int i = 0; i < n; i++)
            {
                if (!visited[i])
                {
                    FillOrder(i);
                }
            }


            // Build the transpose (reverse graph)
            List<int>[] revAdj = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                revAdj[i] = new List<int>();
            }
            // Reverse the edges of the original graph
            for (int u = 0; u < n; u++)
            {
                foreach (int v in _graphAdj[u])
                {
                    revAdj[v].Add(u);
                }
            }

            // Second pass: DFS on the reversed graph in stack order
            Array.Fill(visited, false);
            void DFSUtil(int u, List<int> component)
            {
                visited[u] = true;
                component.Add(u);
                foreach (int v in revAdj[u])
                {
                    if (!visited[v])
                    {
                        DFSUtil(v, component);
                    }
                }
            }

            // Pop top of stack (u) vertice from the stack and get SCCs in reversed graph
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                if (!visited[u])
                {
                    List<int> component = new List<int>(); // dyn list store curr scc
                    DFSUtil(u, component);
                    _stronglyConnectedComponents.Add(component);
                }
            }
        }



        // Runs the currently selected algorithm (Tarjan or Kosaraju) to compute SCCs and records the execution time.
        // Also updates the node color mapping for the resulting components.
        // MAIN FUNCTION to call our SCC algorithms
        private void ComputeSCC()
        {
            // Measure the time taken by the chosen algorithm
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (_useTarjan)
            {
                ComputeTarjanSCCs();
            }
            else
            {
                ComputeKosarajuSCCs();
            }
            stopwatch.Stop();
            _lastAlgorithmTimeMs = stopwatch.Elapsed.TotalMilliseconds;


            // Assign a distinct color to each strongly connected component for visualization
            int nodeCount = _graphAdj.Length;
            _nodeColors = new Color[nodeCount];
            int compIndex = 0;
            foreach (var component in _stronglyConnectedComponents)
            {
                Color color = _sccColors[compIndex % _sccColors.Length];
                foreach (int node in component)
                {
                    _nodeColors[node] = color;
                }
                compIndex++;
            }
        }


        // --- Game Initialization and Setup ---
        // Initialize game content and prepare initial graph data.
        protected override void Initialize()
        {
            // Set up all predefined graphs (nodes and edges)
            InitializeGraphData();
            // Load the first graph and compute its SCCs using the default algorithm
            _currentGraphIndex = 0;
            _graphAdj = _allGraphsAdj[_currentGraphIndex];
            _nodePositions = _allGraphsPositions[_currentGraphIndex];
            ComputeSCC();

            base.Initialize();
        }



        // Defines the nodes and edges for each of the predefined graphs used
        private void InitializeGraphData()
        {

            // professor! if You want to add more graphs or change them, just add them to the _allGraphsAdj and _allGraphsPositions arrays
            // the draw() and update() methods will automatically be able to handle extra inputs. 
            _allGraphsAdj = new List<int>[_totalGraphs][];
            _allGraphsPositions = new Vector2[_totalGraphs][];


            // Graph 1 (index 0): example directed graph
            // Define adjacency list
            _allGraphsAdj[0] = new List<int>[3];
            for (int i = 0; i < 3; i++) _allGraphsAdj[0][i] = new List<int>();
            _allGraphsAdj[0][0].Add(1);
            _allGraphsAdj[0][1].Add(2);
            _allGraphsAdj[0][2].Add(0);
            // Define node positions
            _allGraphsPositions[0] = new Vector2[3];
            _allGraphsPositions[0][0] = new Vector2(200, 150);
            _allGraphsPositions[0][1] = new Vector2(350, 150);
            _allGraphsPositions[0][2] = new Vector2(275, 300);


            // Graph 2 (index 1): another directed graph
            _allGraphsAdj[1] = new List<int>[4];
            for (int i = 0; i < 4; i++) _allGraphsAdj[1][i] = new List<int>();
            _allGraphsAdj[1][0].Add(1);
            _allGraphsAdj[1][1].Add(2);
            _allGraphsAdj[1][2].Add(3);
            _allGraphsAdj[1][3].Add(1);
            // Node positions
            _allGraphsPositions[1] = new Vector2[4];
            _allGraphsPositions[1][0] = new Vector2(150, 200);
            _allGraphsPositions[1][1] = new Vector2(300, 100);
            _allGraphsPositions[1][2] = new Vector2(450, 200);
            _allGraphsPositions[1][3] = new Vector2(300, 300);


            // Graph 3 (index 2): 6 nodes forming two disconnected separate 3-node SCC cycles
            _allGraphsAdj[2] = new List<int>[6];
            _allGraphsPositions[2] = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                _allGraphsAdj[2][i] = new List<int>();
            }
            // First cycle: 0 -> 1 -> 2 -> 0
            _allGraphsAdj[2][0].Add(1);
            _allGraphsAdj[2][1].Add(2);
            _allGraphsAdj[2][2].Add(0);
            // Second cycle: 3 -> 4 -> 5 -> 3
            _allGraphsAdj[2][3].Add(4);
            _allGraphsAdj[2][4].Add(5);
            _allGraphsAdj[2][5].Add(3);
            // Node positions for clear layout
            _allGraphsPositions[2][0] = new Vector2(200, 150);
            _allGraphsPositions[2][1] = new Vector2(300, 100);
            _allGraphsPositions[2][2] = new Vector2(400, 150);
            _allGraphsPositions[2][3] = new Vector2(200, 300);
            _allGraphsPositions[2][4] = new Vector2(300, 350);
            _allGraphsPositions[2][5] = new Vector2(400, 300);


            // Graph 4 (index 3): directed graph example
            _allGraphsAdj[3] = new List<int>[5];
            for (int i = 0; i < 5; i++) _allGraphsAdj[3][i] = new List<int>();
            _allGraphsAdj[3][0].Add(1);
            _allGraphsAdj[3][1].Add(2);
            _allGraphsAdj[3][2].Add(3);
            _allGraphsAdj[3][3].Add(1);
            _allGraphsAdj[3][2].Add(4);
            // Node positions
            _allGraphsPositions[3] = new Vector2[5];
            _allGraphsPositions[3][0] = new Vector2(150, 150);
            _allGraphsPositions[3][1] = new Vector2(300, 80);
            _allGraphsPositions[3][2] = new Vector2(450, 150);
            _allGraphsPositions[3][3] = new Vector2(300, 220);
            _allGraphsPositions[3][4] = new Vector2(550, 300);


            // Graph 5 (index 4): directed graph example
            _allGraphsAdj[4] = new List<int>[6];
            for (int i = 0; i < 6; i++) _allGraphsAdj[4][i] = new List<int>();
            _allGraphsAdj[4][0].Add(1);
            _allGraphsAdj[4][1].Add(2);
            _allGraphsAdj[4][2].Add(0);
            _allGraphsAdj[4][2].Add(3);
            _allGraphsAdj[4][3].Add(4);
            _allGraphsAdj[4][4].Add(5);
            _allGraphsAdj[4][5].Add(3);
            // Node positions
            _allGraphsPositions[4] = new Vector2[6];
            _allGraphsPositions[4][0] = new Vector2(100, 100);
            _allGraphsPositions[4][1] = new Vector2(200, 50);
            _allGraphsPositions[4][2] = new Vector2(300, 100);
            _allGraphsPositions[4][3] = new Vector2(300, 250);
            _allGraphsPositions[4][4] = new Vector2(180, 320);
            _allGraphsPositions[4][5] = new Vector2(420, 320);
        }



        // Load game content (textures, fonts) and initialize drawing resources.
        // called before the first frame is drawn.
        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            // SpriteFont for drawing text (could have an error if "DefaultFont" can't be found but it should be included in the content folder without the need for MGCB)
            _font = Content.Load<SpriteFont>("DefaultFont");


            // 1x1 white pixel texture (for drawing lines)
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });


            // circle texture for drawing nodes
            int diameter = NodeRadius * 2;
            _circleTexture = new Texture2D(GraphicsDevice, diameter, diameter);
            Color[] data = new Color[diameter * diameter];
            Vector2 center = new Vector2(NodeRadius, NodeRadius);
            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    // Set pixel color to white if inside the circle, transparent otherwise
                    Vector2 delta = new Vector2(x - center.X, y - center.Y);
                    if (delta.Length() <= NodeRadius)
                        data[x + y * diameter] = Color.White;
                    else
                        data[x + y * diameter] = Color.Transparent;
                }
            }
            _circleTexture.SetData(data);
        }



        // Unload any non-ContentManager resources if necessary.
        protected override void UnloadContent()
        {
            // realistically I could have unload the textures and fonts here, but It creates a lot of issues
            // so I just leave them loaded for the duration of the program
        }


        // --- Game Loop: Update and Draw ---
        // Handle user input to toggle algorithm or switch graphs, and update game state accordingly.
        // update is called every "tick" which is basically a frame
        protected override void Update(GameTime gameTime)
        {
            // Check for exit request (Escape key)
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                Exit();
            }


            // Get current keyboard state for one-shot key presses
            KeyboardState keyboard = Keyboard.GetState();


            // Toggle algorithm selection when 'A' is pressed
            if (keyboard.IsKeyDown(Keys.A) && !_prevKeyboardState.IsKeyDown(Keys.A))
            {
                _useTarjan = !_useTarjan;
                ComputeSCC();
            }


            // Switch to the next graph when Spacebar is pressed
            if (keyboard.IsKeyDown(Keys.Space) && !_prevKeyboardState.IsKeyDown(Keys.Space))
            {
                _currentGraphIndex = (_currentGraphIndex + 1) % _totalGraphs;
                _graphAdj = _allGraphsAdj[_currentGraphIndex];
                _nodePositions = _allGraphsPositions[_currentGraphIndex];
                ComputeSCC();
            }


            // Remember the keyboard state for the next frame (to detect new key presses)
            // before it could be held down
            _prevKeyboardState = keyboard;
            base.Update(gameTime);
        }



        // Draw the current graph (nodes and directed edges) and UI text (algorithm name, graph index, execution time).
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();


            // Draw all edges with arrowheads
            for (int u = 0; u < _graphAdj.Length; u++)
            {
                foreach (int v in _graphAdj[u])
                {
                    DrawArrow(_nodePositions[u], _nodePositions[v]);
                }
            }


            // Draw all nodes (white filled circles with colored outlines)
            for (int i = 0; i < _nodePositions.Length; i++)
            {
                DrawNode(i, _nodeColors[i]);
            }

            // Show Tarjan's (disc, low) values above each node
            if (_useTarjan && _tarjanDisc != null)
            {
                for (int i = 0; i < _nodePositions.Length; i++)
                {
                    string vals = $"({_tarjanDisc[i]},{_tarjanLow[i]})";
                    Vector2 textSize = _font.MeasureString(vals);
                    // Text position is above the node with an offset
                    Vector2 textPos = _nodePositions[i] - new Vector2(textSize.X / 2, NodeRadius + textSize.Y + 5);
                    _spriteBatch.DrawString(_font, vals, textPos, Color.Cyan);
                }
            }

            // Draw the algorithm and graph info at the top of the screen, most of this is boilerplate code just used to draw the text
            string algoName = _useTarjan ? "Tarjan's" : "Kosaraju's";
            string statusText = $"Algorithm: {algoName} (Press A to toggle)    Graph: {_currentGraphIndex + 1}/{_totalGraphs} (Press Space to next)";
            _spriteBatch.DrawString(_font, statusText, new Vector2(10, 10), Color.Yellow);
            // Draw the execution time of the algorithm
            string timeText = $"Time: {_lastAlgorithmTimeMs:F3} ms";
            _spriteBatch.DrawString(_font, timeText, new Vector2(10, 40), Color.Yellow);


            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}