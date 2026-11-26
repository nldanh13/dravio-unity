using UnityEngine;

namespace Dravio
{
    // World-space emitter follows the animated tail, not the fixed camera.
    public sealed class EmberlingFlameVisual : MonoBehaviour
    {
        [SerializeField] private Transform flameBone;
        [SerializeField] private Light glow;
        [SerializeField] private float intensity = .45f;
        public void Configure(Transform bone, Light light)
        {
            flameBone=bone; glow=light;
        }
        private void LateUpdate()
        {
            if(flameBone==null)return;
            transform.position=flameBone.position;
            transform.rotation=Quaternion.identity;
            if(glow!=null)
                glow.intensity=intensity*(.92f+.08f*Mathf.Sin(Time.time*4.2f)+.04f*Mathf.Sin(Time.time*8.1f));
        }
    }
}
