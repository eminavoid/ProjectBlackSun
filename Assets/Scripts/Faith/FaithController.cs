using System.Collections.Generic;
using UnityEngine;
using System;

namespace Assets.Scripts
{
    public class FaithController : Singleton<FaithController>
    {
        [SerializeField] private PlayerResources resources;
        [SerializeField] private List<Faith> faithStages;

        private Faith currentFaith;

        public static Faith CurrentFaith
        {
            get
            {
                if (Instance.currentFaith == null)
                {
                    Instance.currentFaith = Instance.GetCurrentFaith(Instance.GetZealRatio());
                    return Instance.currentFaith;
                }
                else
                {
                    return Instance.currentFaith;
                }
            }
        }

        public static Action<FaithChangeEvent> onFaithChanged;

        private void Start()
        {
            currentFaith = GetCurrentFaith(GetZealRatio());
        }

        private void OnEnable()
        {
            resources.onResourceGained += OnResourceGained;
        }

        private void OnDisable()
        {
            resources.onResourceGained -= OnResourceGained;
        }

        private void OnResourceGained(Resource resource, int amount)
        {
            if (resource == Resource.Flock || resource == Resource.Zeal)
            {
                UpdateCurrentFaith();
            }
        }

        private float GetZealRatio()
        {
            int zeal = resources.GetResourceAmount(Resource.Zeal);
            int flock = resources.GetResourceAmount(Resource.Flock);

            if (flock == 0) return 0f;
            return zeal / flock;
        }

        private void UpdateCurrentFaith()
        {
            if (resources.GetResourceAmount(Resource.Flock) == 0) return;

            float zealRatio = GetZealRatio();

            Faith newFaith = GetCurrentFaith(zealRatio);
            Faith oldFaith = CurrentFaith;

            currentFaith = newFaith;

            if (newFaith != oldFaith)
            {
                onFaithChanged?.Invoke(new FaithChangeEvent(oldFaith, currentFaith));
            }
        }

        private Faith GetCurrentFaith(float zealRatio)
        {
            foreach (Faith faithStage in faithStages)
            {
                Faith minflockRatioFaith = null;

                bool hasClosestFaithToRatioYet = false;
                ClosestFaithToRatio closestFaithToRatio = new ClosestFaithToRatio();

                if (minflockRatioFaith == null || faithStage.MinZealRatio < minflockRatioFaith.MinZealRatio)
                {
                    minflockRatioFaith = faithStage;
                }

                if (faithStage.MinZealRatio >= zealRatio && (!hasClosestFaithToRatioYet 
                    || HasClosestRatio(zealRatio, faithStage.MinZealRatio, closestFaithToRatio.distance)))
                {
                    closestFaithToRatio = new ClosestFaithToRatio(faithStage, Mathf.Abs(zealRatio - faithStage.MinZealRatio));
                    hasClosestFaithToRatioYet = true;
                }

                if (!hasClosestFaithToRatioYet) return minflockRatioFaith;
                else return closestFaithToRatio.faith;
            }

            throw new Exception("No Faith found");
        }

        private bool HasClosestRatio(float target, float current, float other)
        {
            return Mathf.Abs(target - current) < Mathf.Abs(target - other);
        }

        private readonly struct ClosestFaithToRatio
        {
            public readonly Faith faith;
            public readonly float distance;

            public ClosestFaithToRatio(Faith faith, float distance)
            {
                this.faith = faith;
                this.distance = distance;
            }
        }

        public readonly struct FaithChangeEvent
        {
            public readonly Faith oldFaith;
            public readonly Faith newFaith;

            public FaithChangeEvent(Faith oldFaith, Faith newFaith)
            {
                this.oldFaith = oldFaith;
                this.newFaith = newFaith;
            }
        }
    }
}