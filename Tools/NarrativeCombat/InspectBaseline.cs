var mission = UnityEngine.Object.FindAnyObjectByType<DropletPrototype.MissionController>();
var target = mission.targets[0];
return new {
 scene = mission.gameObject.scene.path,
 total = mission.targets.Length,
 droplet = mission.motor.visualRoot.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Select(f => new { name = f.name, mesh = f.sharedMesh.name, bounds = f.sharedMesh.bounds, readable = f.sharedMesh.isReadable, matrix = f.transform.localToWorldMatrix }).ToArray(),
 turrets = target.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Where(f => f.name.Contains("LOD0_Turret")).Select(f => new { name = f.name, localBounds = f.sharedMesh.bounds, world = f.GetComponent<UnityEngine.Renderer>().bounds, local = target.transform.InverseTransformPoint(f.transform.position), matrix = target.transform.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray()
};
