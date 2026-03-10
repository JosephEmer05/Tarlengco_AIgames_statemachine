using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System;
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform[] patrolPoints;

    [SerializeField] private float detectionRange;
    [SerializeField] private float loseRange;

    [SerializeField] private float waypointTolerance;
    [SerializeField] private float idleAtWaypointSeconds;

    private NavMeshAgent agent;

    private enum NodeStates { Success, Failure, Running }
    //Success 

    private abstract class Node
    {
        public abstract NodeStates Tick();
    }

    private class Selector : Node
    {
        private readonly List<Node> children;
        public Selector (List<Node> children) => this.children = children;
        public override NodeStates Tick()
        {
            foreach (var child in children)
            {
                var state = child.Tick();
                if(state==NodeStates.Success) return NodeStates.Success;
                if(state==NodeStates.Running) return NodeStates.Running;
            }
            return NodeStates.Failure;
        }
    }

    private class Sequence : Node
    {
        private readonly List<Node> children;
        public Sequence(List<Node> children) => this.children = children;
        public override NodeStates Tick()
        {
            foreach (var child in children)
            {
                var state = child.Tick();
                if (state == NodeStates.Failure) return NodeStates.Failure;
                if (state == NodeStates.Running) return NodeStates.Running;
            }
            return NodeStates.Success;
        }
    }

    private class ActionNode : Node
    {
        private readonly Func<NodeStates> action;
        public ActionNode (Func<NodeStates> action) => this.action = action;
        public override NodeStates Tick() => action();
    }

    private bool isChasing;
    private int PatrolIndex;
    private float idleTimer;

    private Node root;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        var chaseSequence = new Sequence(new List<Node>
        {
            new ActionNode(IsTargetDetected),
            new ActionNode(ChaseTarget)
        });

        var patrolSequence = new Sequence(new List<Node>
        {
            new ActionNode(HaspatrolPoints),
            new ActionNode(Patrol)
        });

        var idleAction = new ActionNode(Idle);

        root = new Selector(new List<Node>
        {
            chaseSequence,
            patrolSequence,
            idleAction
        });
    }

    private void Update()
    {
        root.Tick();
    }

    private NodeStates IsTargetDetected()
    {
        if(target == null) return NodeStates.Failure;
        float d = Vector3.Distance(transform.position, target.position);

        if (!isChasing)
        {
            if (d <= detectionRange)
            {
                isChasing = true;
                return NodeStates.Success;
            }
            return NodeStates.Failure;
        }
        else
        {
            if (d <= loseRange) return NodeStates.Success;
            isChasing = false;
            return NodeStates.Failure;
        }
    }

    private NodeStates ChaseTarget()
    {
        if (target == null) return NodeStates.Failure;
        agent.isStopped = false;
        agent.SetDestination(target.position);
        return NodeStates.Running;
    }

    private NodeStates HaspatrolPoints()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return NodeStates.Failure;
        return NodeStates.Success;
    }

   private NodeStates Patrol()
    {
        if (isChasing) return NodeStates.Failure;

        Transform current = patrolPoints[PatrolIndex];
        if (current == null) return NodeStates.Failure;

        if (idleTimer > 0f)
        {
            agent.isStopped = true;
            idleTimer -= Time.deltaTime;
            return NodeStates.Running;
        }

        agent.isStopped = false;
        agent.SetDestination(current.position);

        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            idleTimer = idleAtWaypointSeconds;
            PatrolIndex = (PatrolIndex + 1) % patrolPoints.Length;
        }

        return NodeStates.Running;
    }

    private NodeStates Idle()
    {
        agent.isStopped = true;
        return NodeStates.Running;
    }

    // Visualize detection and lose ranges in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseRange);
    }
}