using System.Collections.Generic;
using Content.Server.Construction.Components;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server.Construction
{
    public partial class ConstructionSystem
    {
        [Dependency] private readonly ContainerSystem _containerSystem = default!;

        private void InitializeGraphs()
        {
        }

        public ConstructionGraphPrototype? GetCurrentGraph(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            return _prototypeManager.TryIndex(construction.GraphIdentifier, out ConstructionGraphPrototype? graph) ? graph : null;
        }

        public ConstructionGraphNode? GetCurrentNode(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.Node is not {} nodeIdentifier)
                return null;

            return GetCurrentGraph(uid, construction) is not {} graph ? null : GetNodeFromGraph(graph, nodeIdentifier);
        }

        public ConstructionGraphEdge? GetCurrentEdge(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (construction.EdgeIndex is not {} edgeIndex)
                return null;

            return GetCurrentNode(uid, construction) is not {} node ? null : GetEdgeFromNode(node, edgeIndex);
        }

        public ConstructionGraphStep? GetCurrentStep(EntityUid uid, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return null;

            if (GetCurrentEdge(uid, construction) is not {} edge)
                return null;

            return GetStepFromEdge(edge, construction.StepIndex);
        }

        public (ConstructionGraphEdge? edge, ConstructionGraphStep? step) GetCurrentEdgeAndStep(EntityUid uid,
            ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction, false))
                return default;

            var edge = GetCurrentEdge(uid, construction);

            if (edge == null)
                return default;

            var step = GetStepFromEdge(edge, construction.StepIndex);

            return (edge, step);
        }

        public ConstructionGraphNode? GetNodeFromGraph(ConstructionGraphPrototype graph, string id)
        {
            return graph.Nodes.TryGetValue(id, out var node) ? node : null;
        }

        public ConstructionGraphEdge? GetEdgeFromNode(ConstructionGraphNode node, int index)
        {
            return node.Edges.Count > index ? node.Edges[index] : null;
        }

        public ConstructionGraphStep? GetStepFromEdge(ConstructionGraphEdge edge, int index)
        {
            return edge.Steps.Count > index ? edge.Steps[index] : null;
        }

        public bool ChangeNode(EntityUid uid, EntityUid? userUid, string id, bool performActions = true, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return false;

            if (GetCurrentGraph(uid, construction) is not {} graph
            ||  GetNodeFromGraph(graph, id) is not {} node)
                return false;

            construction.Node = id;

            if(performActions)
                PerformActions(uid, userUid, node.Actions);

            if(node.Entity is {} newEntity)
                ChangeEntity(uid, userUid, newEntity, construction);

            return true;
        }

        private EntityUid? ChangeEntity(EntityUid uid, EntityUid? userUid, string newEntity,
            ConstructionComponent? construction = null,
            MetaDataComponent? metaData = null,
            ITransformComponent? transform = null,
            ContainerManagerComponent? containerManager = null)
        {
            if (!Resolve(uid, ref construction, ref metaData, ref transform))
                return null;

            if (metaData.EntityPrototype == null || newEntity == metaData.EntityPrototype.ID
            || _prototypeManager.HasIndex<EntityPrototype>(newEntity))
                return null;

            // Optional resolves.
            Resolve(uid, ref containerManager);

            // We create the new entity.
            var newUid = EntityManager.SpawnEntity(newEntity, transform.Coordinates).Uid;

            // Construction transferring.
            var newConstruction = EntityManager.EnsureComponent<ConstructionComponent>(newUid);

            // We set the graph and node accordingly... Then we append our containers to theirs.
            ChangeGraph(newUid, userUid, construction.GraphIdentifier, construction.Node, false, newConstruction);

            // Transfer all construction-owned containers.
            newConstruction.Containers.UnionWith(construction.Containers);

            // Transfer all pending interaction events too.
            while (construction.InteractionQueue.TryDequeue(out var ev))
            {
                newConstruction.InteractionQueue.Enqueue(ev);
            }

            // Transform transferring.
            var newTransform = EntityManager.GetComponent<ITransformComponent>(newUid);
            newTransform.LocalRotation = transform.LocalRotation;
            newTransform.Anchored = transform.Anchored;

            // Container transferring.
            if (containerManager != null)
            {
                // Ensure the new entity has a container manager. Also for resolve goodness.
                var newContainerManager = EntityManager.EnsureComponent<ContainerManagerComponent>(newUid);

                // Transfer all construction-owned containers from the old entity to the new one.
                foreach (var container in construction.Containers)
                {
                    if (!_containerSystem.TryGetContainer(uid, container, out var ourContainer, containerManager))
                        continue;

                    // NOTE: Only Container is supported by Construction!
                    var otherContainer = _containerSystem.EnsureContainer<Container>(newUid, container, newContainerManager);

                    foreach (var entity in ourContainer.ContainedEntities)
                    {
                        ourContainer.ForceRemove(entity);
                        otherContainer.Insert(entity);
                    }
                }
            }

            EntityManager.QueueDeleteEntity(uid);

            if(GetCurrentNode(newUid, newConstruction) is {} node)
                PerformActions(newUid, userUid, node.Actions);

            return newUid;
        }

        public bool ChangeGraph(EntityUid uid, EntityUid? userUid, string graphId, string nodeId, bool performActions = true, ConstructionComponent? construction = null)
        {
            if (!Resolve(uid, ref construction))
                return false;

            if (!_prototypeManager.TryIndex<ConstructionGraphPrototype>(graphId, out var graph))
                return false;

            if(GetNodeFromGraph(graph, nodeId) is not {} node)
                return false;

            construction.GraphIdentifier = graphId;
            return ChangeNode(uid, userUid, nodeId, performActions, construction);
        }
    }
}
