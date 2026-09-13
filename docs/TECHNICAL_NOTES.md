# 技术依据与预算说明

核对日期：2026-09-07。以项目实际版本和行为验证为准。文档中的面数、数量与 64 MiB 预算是本轮设计目标，不是平台保证或已经测试的结果。

- Blender linked duplicate 共享网格数据，实例变换可独立。Blender 4.5 Manual: https://docs.blender.org/manual/sl/4.5/scene_layout/object/editing/duplicate_linked.html
- LOD 主要降低绘制负载；这不等于自动卸载其他 LOD 资源。Unity 6.5 Manual: https://docs.unity3d.com/6000.5/Documentation/Manual/LevelOfDetail.html
- Mesh Read/Write 开启时保留 CPU 可访问副本。Unity API: https://docs.unity3d.com/ScriptReference/Mesh-isReadable.html
- Static batching 会引入额外的合并网格 CPU 内存，即使原对象共享同一网格。Unity 6 Manual: https://docs.unity3d.com/6000.0/Documentation/Manual/static-batching-enable.html
- Unity 建议显式导出 FBX，而不是在生产中依赖 .blend 等专有源格式导入。Unity 6.5 Manual: https://docs.unity3d.com/6000.5/Documentation/Manual/3D-formats.html

参考图是本次对话中生成的视觉概念图，不是测绘资料，也不构成对原著天体距离、比例或小行星分布的考证。
