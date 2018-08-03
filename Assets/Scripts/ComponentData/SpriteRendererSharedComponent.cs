using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Entities;
using Unity.Mathematics;

using UnityEngine;

namespace Wahren
{
    struct SpriteRendererSharedComponent : ISharedComponentData
    {
        public Mesh mesh;
        public Material material;

        public SpriteRendererSharedComponent(Mesh mesh, Material material)
        {
            this.mesh = mesh;
            this.material = material;
        }
        public SpriteRendererSharedComponent(Sprite sprite, Material material)
        {
            mesh = CreateMesh(sprite);
            this.material = material;
        }
        public SpriteRendererSharedComponent(Sprite sprite, Material material, float x, float y)
        {
            mesh = CreateMesh(sprite, x, y);
            this.material = material;
        }
        public SpriteRendererSharedComponent(Shader shader, Sprite sprite) : this(CreateMesh(sprite), new Material(shader)
        {
            mainTexture = sprite.texture,
            enableInstancing = true,
        })
        { }
        public SpriteRendererSharedComponent(Shader shader, Sprite sprite, float x, float y) : this(CreateMesh(sprite, x, y), new Material(shader)
        {
            mainTexture = sprite.texture,
            enableInstancing = true,
        })
        { }
        public static Mesh CreateMesh(Sprite sprite)
        {
            if (object.ReferenceEquals(sprite, null))
                throw new ArgumentNullException();
            var mesh = new Mesh();
            var verticesArray = sprite.vertices;
            var verticesList = new List<Vector3>(verticesArray.Length);
            for (int i = 0; i < verticesArray.Length; i++)
                verticesList.Add((Vector3)verticesArray[i]);
            mesh.SetVertices(verticesList);
            var uvsArray = sprite.uv;
            var uvsList = new List<Vector2>(uvsArray.Length);
            uvsList.AddRange(uvsArray);
            mesh.SetUVs(0, uvsList);
            mesh.SetTriangles(Array.ConvertAll(sprite.triangles, c => (int)c), 0);
            return mesh;
        }
        public static Mesh CreateMesh(Sprite sprite, float x, float y)
        {
            if (x < 0 || x > 1 || y < 0 || y > 1) throw new ArgumentOutOfRangeException();
            var mesh = new Mesh();
            var verticesArray = sprite.vertices;
            var verticesList = new List<Vector3>(verticesArray.Length);
            float xMin = 0, xMax = 0, yMin = 0, yMax = 0;
            for (int i = 0; i < verticesArray.Length; i++)
            {
                var vert = verticesArray[i];
                if (vert.x < xMin)
                    xMin = vert.x;
                else if (vert.x > xMax)
                    xMax = vert.x;
                if (vert.y < yMin)
                    yMin = vert.y;
                else if (vert.y > yMax)
                    yMax = vert.y;
            }
            var subX = -math.lerp(xMin, xMax, x);
            var subY = -math.lerp(yMin, yMax, y);
            for (int i = 0; i < verticesArray.Length; i++)
            {
                var item = (Vector3)verticesArray[i];
                item.x += subX;
                item.y += subY;
                verticesList.Add(item);
            }
            mesh.SetVertices(verticesList);
            var uvsArray = sprite.uv;
            var uvsList = new List<Vector2>(uvsArray.Length);
            uvsList.AddRange(uvsArray);
            mesh.SetUVs(0, uvsList);
            mesh.SetTriangles(Array.ConvertAll(sprite.triangles, c => (int)c), 0);
            return mesh;
        }
    }
}