# HybridRenderer

Hybrid Deferred Rendering Pipeline built using Unity's SRP feature.
Video Link: https://www.youtube.com/watch?v=TXqKqrPcdZQ

Features:
- Realtime Raytraced Mirror Specular & Diffuse GI (DDGI)
- Raytraced Shadows
- Raytraced AO
- Voxel Light Culling
- Sky Rendering (Precomputed Atmospheric Scattering)
- Unified Deferred Lighting Path

Info:
- All raytracing and compute shaders are located in the "Resources" folder.
- "Shaders/Lighting.cginc" contains light loop
- "Shaders/Standard.shader" is gbuffer shader (raster + raytraced)
- "Resources/DeferredLighting.raytrace" is main lighting shader

Resources:
- Unity SRP: https://docs.unity3d.com/2019.3/Documentation/Manual/ScriptableRenderPipeline.html
- DDGI Algorithm: https://morgan3d.github.io/articles/2019-04-01-ddgi/
- Precomputed Atmospheric Scattering: https://hal.inria.fr/inria-00288758/document
- Car Model is From Here: https://sketchfab.com/3d-models/2021-lamborghini-countach-lpi-800-4-d76b94884432422b966d1a7f8815afb5

<img width="1112" alt="Screen2" src="https://user-images.githubusercontent.com/7034703/158116921-f1f879a4-56ae-4c78-bdbf-81ea95d6d495.PNG">
<img width="1111" alt="Screen1" src="https://user-images.githubusercontent.com/7034703/158116928-4a96785f-a9da-421a-8c5e-f528cedfec74.PNG">
<img width="1114" alt="Screen3" src="https://user-images.githubusercontent.com/7034703/158116931-c8b1d321-198a-48b7-9462-7e0afe6255af.PNG">
<img width="1110" alt="Atmosphere" src="https://user-images.githubusercontent.com/7034703/158116941-5967757e-1d5e-4603-8611-342d095d8bad.PNG">
