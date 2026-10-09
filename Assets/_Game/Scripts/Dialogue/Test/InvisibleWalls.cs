using UnityEngine;

namespace Hortensia.Runtime
{
    [RequireComponent(typeof(Renderer))]
    public sealed class InvisibleWalls : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float height = 5f;
        [SerializeField, Min(0.1f)] private float thickness = 1f;

        private void Awake()
        {
            Bounds b = GetComponent<Renderer>().bounds;
            float y = b.max.y + height * 0.5f;
            AddWall("Wall North", new Vector3(b.center.x, y, b.max.z + thickness * 0.5f), new Vector3(b.size.x + thickness * 2f, height, thickness));
            AddWall("Wall South", new Vector3(b.center.x, y, b.min.z - thickness * 0.5f), new Vector3(b.size.x + thickness * 2f, height, thickness));
            AddWall("Wall East", new Vector3(b.max.x + thickness * 0.5f, y, b.center.z), new Vector3(thickness, height, b.size.z));
            AddWall("Wall West", new Vector3(b.min.x - thickness * 0.5f, y, b.center.z), new Vector3(thickness, height, b.size.z));
        }

        private void AddWall(string wallName, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(wallName);
            wall.transform.position = position;
            wall.transform.rotation = Quaternion.identity;
            wall.AddComponent<BoxCollider>().size = size;
        }
    }
}
