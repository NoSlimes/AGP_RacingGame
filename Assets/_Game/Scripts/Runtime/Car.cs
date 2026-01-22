using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
#endif

namespace RacingGame
{
    [RequireComponent(typeof(Rigidbody), typeof(CarInputComponent), typeof(CarControl))]
    public class Car : TickableBehaviour
    {
        private readonly Dictionary<Type, List<ICarComponent>> carComponentLookup = new();
        private readonly List<ICarComponent> carComponents = new();

        public Rigidbody Rigidbody { get; private set; }

        public float ProgressScore { get; set; }
        public string CarName { get; private set; }

        public T GetCarComponent<T>()
        {
            return carComponentLookup.TryGetValue(typeof(T), out List<ICarComponent> cL) ? (T)cL.First() : default;
        }

        public T[] GetCarComponents<T>()
        {
            return carComponentLookup.TryGetValue(typeof(T), out List<ICarComponent> cL) ? cL.Cast<T>().ToArray() : default;
        }

        private void Awake()
        {
            FindComponents();
        }

        private void FindComponents()
        {
            carComponentLookup.Clear();
            carComponents.Clear();

            foreach (ICarComponent c in GetComponentsInChildren<ICarComponent>())
            {
                if (!carComponentLookup.TryGetValue(c.GetType(), out List<ICarComponent> cL))
                {
                    carComponentLookup[c.GetType()] = new();
                }

                carComponentLookup[c.GetType()].Add(c);
                carComponents.Add(c);
            }

            carComponents.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            Rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            foreach (ICarComponent c in carComponents)
                c.Initialize(this);
        }

        public override void Tick()
        {
            base.Tick();

            for (int i = 0; i < carComponents.Count; i++)
            {
                ICarComponent c = carComponents[i];
                c.TickComponent();
            }
        }

        public override void FixedTick()
        {
            base.FixedTick();

            for (int i = 0; i < carComponents.Count; i++)
            {
                ICarComponent c = carComponents[i];
                c.FixedTickComponent();
            }
        }

        public void SetName(string name)
        {
            CarName = name;
            gameObject.name = $"Car {name}";
        }
    }

#if UNITY_EDITOR
#endif

}
