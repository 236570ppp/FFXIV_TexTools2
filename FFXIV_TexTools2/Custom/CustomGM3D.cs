﻿// FFXIV TexTools
// Copyright © 2017 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Utilities;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FFXIV_TexTools2.Shader
{
    public class CustomGM3D : CustomMaterialGeometryModel3D
    {
        private DefaultVertex[] vertexArrayBuffer = null;
        private readonly ImmutableBufferProxy<DefaultVertex> vertexBuffer = new ImmutableBufferProxy<DefaultVertex>(DefaultVertex.SizeInBytes, BindFlags.VertexBuffer);
        private readonly ImmutableBufferProxy<int> indexBuffer = new ImmutableBufferProxy<int>(sizeof(int), BindFlags.IndexBuffer);
        /// <summary>
        /// For subclass override
        /// </summary>
        public override IBufferProxy VertexBuffer
        {
            get
            {
                return vertexBuffer;
            }
        }
        /// <summary>
        /// For subclass override
        /// </summary>
        public override IBufferProxy IndexBuffer
        {
            get
            {
                return indexBuffer;
            }
        }

        protected override void OnRasterStateChanged()
        {
            Disposer.RemoveAndDispose(ref this.rasterState);
            if (!IsAttached) { return; }
            // --- set up rasterizer states
            var rasterStateDesc = new RasterizerStateDescription()
            {
                FillMode = FillMode,
                CullMode = CullMode.Front,
                DepthBias = DepthBias,
                DepthBiasClamp = 0.0f,
                SlopeScaledDepthBias = 0.0f,
                IsDepthClipEnabled = true,
                IsFrontCounterClockwise = false,
                IsMultisampleEnabled = IsMultisampleEnabled,
                IsAntialiasedLineEnabled = true,
                IsScissorEnabled = IsThrowingShadow ? false : IsScissorEnabled

            };
            try
            {
                this.rasterState = new RasterizerState(this.Device, rasterStateDesc);
            }
            catch (System.Exception)
            {
            }
        }

        protected override void OnGeometryChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnGeometryChanged(e);
        }

        protected override void OnGeometryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnGeometryPropertyChanged(sender, e);
            if (sender is MeshGeometry3D)
            {
                if (e.PropertyName.Equals(nameof(MeshGeometry3D.TextureCoordinates)))
                {
                    OnUpdateVertexBuffer(UpdateTextureOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Positions)))
                {
                    OnUpdateVertexBuffer(UpdatePositionOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Colors)))
                {
                    OnUpdateVertexBuffer(UpdateColorsOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Indices)) || e.PropertyName.Equals(Geometry3D.TriangleBuffer))
                {
                    indexBuffer.CreateBufferFromDataArray(this.Device, this.geometryInternal.Indices);
                    InvalidateRender();
                }
                else if (e.PropertyName.Equals(Geometry3D.VertexBuffer))
                {
                    OnUpdateVertexBuffer(CreateDefaultVertexArray);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        protected override bool OnAttach(IRenderHost host)
        {
            // --- attach
            if (!base.OnAttach(host))
            {
                return false;
            }

            // --- get variables
            this.vertexLayout = renderHost.EffectsManager.GetLayout(this.renderTechnique);
            this.effectTechnique = effect.GetTechniqueByName(this.renderTechnique.Name);

            // --- transformations
            this.effectTransforms = new EffectTransformVariables(this.effect);

            // --- material 
            this.AttachMaterial();

            // --- scale texcoords
            var texScale = TextureCoodScale;

            // --- get geometry
            var geometry = this.geometryInternal as MeshGeometry3D;

            // -- set geometry if given
            if (geometry != null)
            {
                //throw new HelixToolkitException("Geometry not found!");                

                // --- init vertex buffer
                CreateVertexBuffer(CreateDefaultVertexArray);
                indexBuffer.CreateBufferFromDataArray(this.Device, geometry.Indices);
            }
            else
            {
                throw new System.Exception("Geometry must not be null");
            }
            // --- flush
            //this.Device.ImmediateContext.Flush();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnUpdateVertexBuffer(Func<DefaultVertex[]> updateFunction)
        {
            CreateVertexBuffer(updateFunction);
            InvalidateRender();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateVertexBuffer(Func<DefaultVertex[]> updateFunction)
        {
            // --- get geometry
            var geometry = this.geometryInternal as MeshGeometry3D;

            // -- set geometry if given
            if (geometry != null && geometry.Positions != null)
            {
                var data = updateFunction();
                vertexBuffer.CreateBufferFromDataArray(this.Device, data);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDetach()
        {
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            base.OnDetach();
        }

        protected override bool CanRender(RenderContext context)
        {
            return base.CanRender(context) && this.effectMaterial != null;
        }

        protected override void OnRender(RenderContext renderContext)
        {
            // --- set constant paramerers             
            var worldMatrix = this.modelMatrix * renderContext.WorldMatrix;
            this.effectTransforms.mWorld.SetMatrix(ref worldMatrix);

            // --- check shadowmaps
            this.hasShadowMap = this.renderHost.IsShadowMapEnabled;
            this.effectMaterial.bHasShadowMapVariable.Set(this.hasShadowMap);

            // --- set material params      
            this.effectMaterial.AttachMaterial();

            this.bHasInstances.Set(this.hasInstances);
            // --- set context
            renderContext.DeviceContext.InputAssembler.InputLayout = this.vertexLayout;
            renderContext.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            renderContext.DeviceContext.InputAssembler.SetIndexBuffer(this.IndexBuffer.Buffer, Format.R32_UInt, 0);

            // --- set rasterstate            
            renderContext.DeviceContext.Rasterizer.State = this.rasterState;
            if (this.hasInstances)
            {
                // --- update instance buffer
                if (this.isInstanceChanged)
                {
                    InstanceBuffer.UploadDataToBuffer(renderContext.DeviceContext, this.instanceInternal);
                    this.isInstanceChanged = false;
                }

                // --- INSTANCING: need to set 2 buffers            
                renderContext.DeviceContext.InputAssembler.SetVertexBuffers(0, new[]
                {
                    new VertexBufferBinding(this.VertexBuffer.Buffer, this.VertexBuffer.StructureSize, 0),
                    new VertexBufferBinding(this.InstanceBuffer.Buffer, this.InstanceBuffer.StructureSize, 0),
                });

                // --- render the geometry
                this.effectTechnique.GetPassByIndex(0).Apply(renderContext.DeviceContext);
                // --- draw
                renderContext.DeviceContext.DrawIndexedInstanced(this.geometryInternal.Indices.Count, this.instanceInternal.Count, 0, 0, 0);
                this.bHasInstances.Set(false);
            }
            else
            {
                // --- bind buffer                
                renderContext.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(this.VertexBuffer.Buffer, this.VertexBuffer.StructureSize, 0));
                // --- render the geometry
                // 
                var pass = this.effectTechnique.GetPassByIndex(0);
                pass.Apply(renderContext.DeviceContext);
                // --- draw
                renderContext.DeviceContext.DrawIndexed(this.geometryInternal.Indices.Count, 0, 0);
            }
        }

        protected override bool CanHitTest(IRenderMatrices context)
        {
            return base.CanHitTest(context) && geometryInternal is MeshGeometry3D && geometryInternal.Indices != null && geometryInternal.Indices.Count > 0;
        }

        protected override bool OnHitTest(IRenderMatrices context, Ray rayWS, ref List<HitTestResult> hits)
        {
            var g = this.geometryInternal as MeshGeometry3D;
            bool isHit = false;

            if (g.Octree != null)
            {
                isHit = g.Octree.HitTest(context, this, ModelMatrix, rayWS, ref hits);
            }
            else
            {
                var result = new HitTestResult();
                result.Distance = double.MaxValue;
                if (g != null)
                {
                    var m = this.modelMatrix;

                    // put bounds to world space
                    var b = BoundingBox.FromPoints(this.Bounds.GetCorners().Select(x => Vector3.TransformCoordinate(x, m)).ToArray());

                    //var b = this.Bounds;

                    // this all happens now in world space now:
                    if (rayWS.Intersects(ref b))
                    {
                        int index = 0;
                        foreach (var t in g.Triangles)
                        {
                            float d;
                            var p0 = Vector3.TransformCoordinate(t.P0, m);
                            var p1 = Vector3.TransformCoordinate(t.P1, m);
                            var p2 = Vector3.TransformCoordinate(t.P2, m);
                            if (Collision.RayIntersectsTriangle(ref rayWS, ref p0, ref p1, ref p2, out d))
                            {
                                if (d > 0 && d < result.Distance) // If d is NaN, the condition is false.
                                {
                                    result.IsValid = true;
                                    result.ModelHit = this;
                                    // transform hit-info to world space now:
                                    result.PointHit = (rayWS.Position + (rayWS.Direction * d)).ToPoint3D();
                                    result.Distance = d;

                                    var n = Vector3.Cross(p1 - p0, p2 - p0);
                                    n.Normalize();
                                    // transform hit-info to world space now:
                                    result.NormalAtHit = n.ToVector3D();// Vector3.TransformNormal(n, m).ToVector3D();
                                    result.TriangleIndices = new System.Tuple<int, int, int>(g.Indices[index], g.Indices[index + 1], g.Indices[index + 2]);
                                    result.Tag = index / 3;
                                    isHit = true;
                                }
                            }
                            index += 3;
                        }
                    }
                }
                if (isHit)
                {
                    hits.Add(result);
                }
            }
            return isHit;
        }

        /// <summary>
        /// Creates a <see cref="T:DefaultVertex[]"/>.
        /// </summary>
        private DefaultVertex[] CreateDefaultVertexArray()
        {
            var geometry = (MeshGeometry3D)this.geometryInternal;
            var positions = geometry.Positions.GetEnumerator();
            var vertexCount = geometry.Positions.Count;

            var colors = geometry.Colors != null ? geometry.Colors.GetEnumerator() : Enumerable.Repeat(Color4.White, vertexCount).GetEnumerator();
            var textureCoordinates = geometry.TextureCoordinates != null ? geometry.TextureCoordinates.GetEnumerator() : Enumerable.Repeat(Vector2.Zero, vertexCount).GetEnumerator();
            var texScale = this.TextureCoodScale;
            var normals = geometry.Normals != null ? geometry.Normals.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
            var tangents = geometry.Tangents != null ? geometry.Tangents.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
            var bitangents = geometry.BiTangents != null ? geometry.BiTangents.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
            if (!ReuseVertexArrayBuffer || vertexArrayBuffer == null || vertexArrayBuffer.Length < vertexCount)
                vertexArrayBuffer = new DefaultVertex[vertexCount];

            for (var i = 0; i < vertexCount; i++)
            {
                positions.MoveNext();
                colors.MoveNext();
                textureCoordinates.MoveNext();
                normals.MoveNext();
                tangents.MoveNext();
                bitangents.MoveNext();
                vertexArrayBuffer[i].Position = new Vector4(positions.Current, 1f);
                vertexArrayBuffer[i].Color = colors.Current;
                vertexArrayBuffer[i].TexCoord = textureCoordinates.Current * texScale;
                vertexArrayBuffer[i].Normal = normals.Current;
                vertexArrayBuffer[i].Tangent = tangents.Current;
                vertexArrayBuffer[i].BiTangent = bitangents.Current;
            }

            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdateTextureOnly()
        {
            var geometry = (MeshGeometry3D)this.geometryInternal;
            var vertexCount = geometry.Positions.Count;
            var texScale = this.TextureCoodScale;
            if (vertexArrayBuffer != null && geometry.TextureCoordinates != null && vertexArrayBuffer.Length >= vertexCount)
            {
                var textureCoordinates = geometry.TextureCoordinates != null && geometry.TextureCoordinates.Count == vertexCount ?
                    geometry.TextureCoordinates.GetEnumerator() : Enumerable.Repeat(Vector2.Zero, vertexCount).GetEnumerator();

                for (int i = 0; i < vertexCount; ++i)
                {
                    textureCoordinates.MoveNext();
                    vertexArrayBuffer[i].TexCoord = textureCoordinates.Current * texScale;
                }
            }
            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdatePositionOnly()
        {
            var geometry = (MeshGeometry3D)this.geometryInternal;
            var vertexCount = geometry.Positions.Count;
            if (vertexArrayBuffer != null && vertexArrayBuffer.Length >= vertexCount)
            {
                var positions = geometry.Positions.GetEnumerator();
                var normals = geometry.Normals != null ? geometry.Normals.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
                var tangents = geometry.Tangents != null ? geometry.Tangents.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
                var bitangents = geometry.BiTangents != null ? geometry.BiTangents.GetEnumerator() : Enumerable.Repeat(Vector3.Zero, vertexCount).GetEnumerator();
                for (int i = 0; i < vertexCount; ++i)
                {
                    positions.MoveNext();
                    normals.MoveNext();
                    tangents.MoveNext();
                    bitangents.MoveNext();
                    vertexArrayBuffer[i].Position = new Vector4(positions.Current, 1f);
                    vertexArrayBuffer[i].Normal = normals.Current;
                    vertexArrayBuffer[i].Tangent = tangents.Current;
                    vertexArrayBuffer[i].BiTangent = bitangents.Current;
                }
            }
            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdateColorsOnly()
        {
            var vertexCount = geometryInternal.Positions.Count;
            if (vertexArrayBuffer != null && geometryInternal.Colors != null && vertexArrayBuffer.Length >= vertexCount)
            {
                var colors = geometryInternal.Colors != null && geometryInternal.Colors.Count == vertexCount ?
                    geometryInternal.Colors.GetEnumerator() : Enumerable.Repeat(Color4.White, vertexCount).GetEnumerator();

                for (int i = 0; i < vertexCount; ++i)
                {
                    colors.MoveNext();
                    vertexArrayBuffer[i].Color = colors.Current;
                }
            }
            return vertexArrayBuffer;
        }

    }
}
