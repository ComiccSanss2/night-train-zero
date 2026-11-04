using UnityEngine;

namespace Subnet.Gameplay
{
    /// <summary>A single glowing pixel cube that drifts upward, wobbles and shrinks away.</summary>
    public class PixelMote : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Vector3 velocity;
        private float life;
        private float maxLife;
        private float startSize;
        private float wobbleSeed;

        public static void Spawn(Vector3 position, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PixelMote";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(EmissionId, color * 2f);
            renderer.SetPropertyBlock(block);

            var mote = go.AddComponent<PixelMote>();
            mote.startSize = Random.Range(0.05f, 0.12f);
            go.transform.localScale = Vector3.one * mote.startSize;
            mote.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.6f), Random.Range(-0.3f, 0.3f));
            mote.maxLife = mote.life = Random.Range(1.2f, 2.4f);
            mote.wobbleSeed = Random.value * 10f;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            float t = Time.time + wobbleSeed;
            transform.position += (velocity + new Vector3(Mathf.Sin(t * 3f) * 0.2f, 0f, Mathf.Cos(t * 2.3f) * 0.2f)) * Time.deltaTime;
            transform.localScale = Vector3.one * (startSize * (life / maxLife));
            transform.Rotate(40f * Time.deltaTime, 70f * Time.deltaTime, 0f);
        }
    }
}
