using System;
using System.Collections.Generic;
using System.Linq;
using _.Scripts.Gameplay;
using _.Scripts.Utility.GameObject;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _.Scripts.Editor.UI_Toolkit
{
    public class ArrowDataManager : IDisposable
    {
        private List<SessionPreset.ArrowNode>             _arrowNodes;
        private Dictionary<int2, SessionPreset.ArrowNode> _arrowNodesMatrix;
        private List<ArrowElement>                        _arrowElementsCache;
        private Material                                  _targetMaterial;
        private ComputeBuffer                             _computeBuffer;
        private const int MaxLinkedNodeIterations = 1024;

        public IReadOnlyList<SessionPreset.ArrowNode> ArrowNodes => _arrowNodes;

        public ComputeBuffer ComputeBuffer => _computeBuffer;

        public ArrowDataManager()
        {
            _arrowNodes         = new List<SessionPreset.ArrowNode>();
            _arrowNodesMatrix   = new Dictionary<int2, SessionPreset.ArrowNode>();
            _arrowElementsCache = new List<ArrowElement>();
        }

        public void LoadPreset(SessionPreset.ArrowNode[] nodes)
        {
            Clear();

            if (nodes == null || nodes.Length == 0)
                return;

            LoadPreset((IEnumerable<SessionPreset.ArrowNode>)nodes);
        }

        private void LoadPreset(IEnumerable<SessionPreset.ArrowNode> nodes)
        {
            if (nodes == null)
                return;

            _arrowNodes.Clear();
            _arrowNodes.AddRange(nodes);

            RebuildMatrixAndCache();
            UpdateComputeBuffer(_targetMaterial);
        }

        public bool CanCreateArrow(Stack<int3> selectedNodes)
        {
            if (selectedNodes == null)
                return true;

            foreach (var node in selectedNodes)
            {
                if (_arrowNodesMatrix.ContainsKey(node.xy))
                    return false;
            }

            return true;
        }

        public bool CanCreateArrow(IReadOnlyList<int3> selectedNodes)
        {
            if (selectedNodes == null)
                return true;

            foreach (var node in selectedNodes)
            {
                if (_arrowNodesMatrix.ContainsKey(node.xy))
                    return false;
            }

            return true;
        }

        public void AddArrowPath(List<int3> path)
        {
            if (path == null || path.Count == 0)
                return;

            SessionPreset.ArrowNode head = default;
            SessionPreset.ArrowNode tail = default;

            for (int i = path.Count - 1; i >= 0; i--)
            {
                SessionPreset.ArrowNode node = new SessionPreset.ArrowNode
                {
                    index    = path[i].xy,
                    previous = tail,
                    next     = null
                };

                if (tail != null)
                    tail.next = node;

                tail = node;

                if (i == path.Count - 1)
                    head = node;
            }

            _arrowNodes.Add(head);

            RebuildMatrixAndCache();
            UpdateComputeBuffer(_targetMaterial);
        }

        public bool RemoveNodeAt(int2 gridIndex)
        {
            if (!_arrowNodesMatrix.TryGetValue(gridIndex, out var node))
                return false;

            SessionPreset.ArrowNode previous = node.previous;
            SessionPreset.ArrowNode next = node.next;

            _arrowNodesMatrix.Remove(gridIndex);

            if (previous != null && next != null)
            {
                previous.next = null;
                node.previous = null;
                next.previous = null;

                _arrowNodes.Remove(node);

                if (!_arrowNodes.Contains(next))
                    _arrowNodes.Add(next);
            }
            else
            {
                node.previous = null;
                node.next = null;
                _arrowNodes.Remove(node);

                if (previous != null)
                {
                    var previousRoot = GetNodeRoot(previous);
                    if (TryGetPathLength(previousRoot, out int previousLength) && previousLength <= 1)
                        _arrowNodes.Remove(previousRoot);
                }
            }

            ValidateAndCleanPaths();

            return true;
        }

        public void ValidateAndCleanPaths()
        {
            foreach (var node in _arrowNodes.ToArray())
            {
                var root = GetNodeRoot(node);

                if (!TryGetPathLength(root, out int length))
                {
                    _arrowNodes.Remove(node);
                    continue;
                }

                if (length < 1 && _arrowNodes.Contains(root))
                    _arrowNodes.Remove(root);
            }

            RebuildMatrixAndCache();
            UpdateComputeBuffer(_targetMaterial);
        }

        public void UpdateComputeBuffer(Material targetMaterial)
        {
            if (_targetMaterial != null && _targetMaterial != targetMaterial)
            {
                _targetMaterial.SetInt("_ArrowsDataSize", 0);
                _targetMaterial.SetBuffer("_ArrowsData", (ComputeBuffer)null);
            }

            _targetMaterial = targetMaterial;

            if (_targetMaterial == null)
                return;

            if (_computeBuffer != null && _computeBuffer.IsValid())
                _computeBuffer.Dispose();

            if (_arrowElementsCache.Count > 0)
            {
                _computeBuffer = new ComputeBuffer(_arrowElementsCache.Count, ArrowElement.GetStrideSize());
                _computeBuffer.SetData(_arrowElementsCache.ToArray());

                _targetMaterial.SetInt("_ArrowsDataSize", _computeBuffer.count);
                _targetMaterial.SetBuffer("_ArrowsData", _computeBuffer);
            }
            else
            {
                _computeBuffer = null;
                _targetMaterial.SetInt("_ArrowsDataSize", 0);
                _targetMaterial.SetBuffer("_ArrowsData", (ComputeBuffer)null);
            }
        }

        public void Clear()
        {
            if (_computeBuffer != null && _computeBuffer.IsValid())
            {
                _computeBuffer.Dispose();
                _computeBuffer = null;
            }

            _arrowNodes.Clear();
            _arrowNodesMatrix.Clear();
            _arrowElementsCache.Clear();

            if (_targetMaterial != null)
            {
                _targetMaterial.SetInt("_ArrowsDataSize", 0);
                _targetMaterial.SetBuffer("_ArrowsData", (ComputeBuffer)null);
            }
        }

        public void Dispose()
        {
            if (_targetMaterial != null)
            {
                _targetMaterial.SetInt("_ArrowsDataSize", 0);
                _targetMaterial.SetBuffer("_ArrowsData", (ComputeBuffer)null);
                _targetMaterial = null;
            }

            if (_computeBuffer != null && _computeBuffer.IsValid())
            {
                _computeBuffer.Dispose();
                _computeBuffer = null;
            }

            _arrowNodes.Clear();
            _arrowNodesMatrix.Clear();
            _arrowElementsCache.Clear();
        }

        private static bool TryGetPathLength(SessionPreset.ArrowNode root, out int length)
        {
            length = 0;
            var visited = new HashSet<SessionPreset.ArrowNode>();

            while (root != null)
            {
                if (!visited.Add(root) || length >= MaxLinkedNodeIterations)
                {
                    Debug.LogWarning("ArrowDataManager detected a corrupted arrow node chain and stopped traversal.");
                    return false;
                }

                length++;
                root = root.next;
            }

            return true;
        }

        private static SessionPreset.ArrowNode GetNodeRoot(SessionPreset.ArrowNode root)
        {
            var visited = new HashSet<SessionPreset.ArrowNode>();

            while (root.previous != null)
            {
                if (!visited.Add(root) || visited.Count >= MaxLinkedNodeIterations)
                {
                    Debug.LogWarning("ArrowDataManager detected a corrupted previous-link arrow node chain and stopped traversal.");
                    return root;
                }

                root = root.previous;
            }

            return root;
        }

        private void RebuildMatrixAndCache()
        {
            _arrowElementsCache.Clear();
            _arrowNodesMatrix.Clear();

            foreach (var node in _arrowNodes)
            {
                foreach (var element in node.Flatten())
                {
                    if (!_arrowNodesMatrix.ContainsKey(element.index))
                        _arrowNodesMatrix.Add(element.index, element);
                }
            }

            _arrowElementsCache.AddRange(_arrowNodesMatrix.Select(kv => new ArrowElement
            {
                elementType            = kv.Value.GetElementType(),
                elementDirection       = kv.Value.GetElementDirection(out bool negate),
                elementDirectionNegate = negate ? 1 : 0,
                elementPoints          = kv.Value.GetElementPoints()
            }));
        }

    }
}