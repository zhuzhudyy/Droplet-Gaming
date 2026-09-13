"""Summarize actual attributed SRP events without inventing instance multipliers."""
import json
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[3]
evidence = root / 'docs/verification/SpaceEnvironment'
rows = []
audit = json.loads((evidence / 'unity-audit.json').read_text(encoding='utf-8-sig'))
estimates = {v['camera']: v['estimatedVisibleTriangles'] for v in audit['views']}
for camera in ['ReferenceCamera', 'SideCamera', 'TopCamera', 'GameplayTurnCamera', 'BeltCloseCamera']:
    path = evidence / f'frame-{camera}.json'
    if not path.exists():
        rows.append(dict(camera=camera, allEventDataValid=False, submittedEnvironmentTriangles=None,
            analyticalTriangleEstimate=estimates[camera], reason='No valid actual Frame Debugger capture available. Native LOD and saved image inspection are separate checks.'))
        continue
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    if not (data['completed'] and data['allEventDataValid']):
        rows.append(dict(camera=camera, allEventDataValid=False, submittedEnvironmentTriangles=None,
            analyticalTriangleEstimate=estimates[camera], reason='Actual capture unavailable/stale; invalid events deliberately excluded from measured totals.',
            source=path.relative_to(root).as_posix()))
        continue
    assert len(data['cameras']) == 1 and data['cameras'][0]['name'] == camera
    draws = [e for e in data['events'] if e['attributedToEnvironment']]
    assert draws and all(e['type'] == 'SRPBatch' for e in draws)
    assert all(e['indexCount'] >= 0 and e['indexCount'] % 3 == 0 for e in draws)
    assert all(e['allBatchMeshPathsAreEnvironment'] for e in draws)
    assert all(e['passName'] in ['Unlit', 'ForwardLit'] for e in data['events']), 'Unexpected pass; inspect it before attribution.'
    indices = sum(e['indexCount'] for e in draws)
    calls = sum(e['drawCallCount'] for e in draws)
    rows.append(dict(camera=camera, captureUtc=data['utc'], context=data['context'],
        cameraSettings=data['cameras'][0], allEventDataValid=True,
        eventCount=data['eventCount'], srpBatchEvents=len(draws),
        environmentDrawCalls=calls, submittedEnvironmentTriangles=indices // 3,
        analyticalTriangleEstimate=estimates[camera],
        rawIndexCount=indices, adjacentGameViewTriangles=data['gameViewStatsTrianglesAtBegin'],
        adjacentGameViewDrawCalls=data['gameViewStatsDrawCallsAtBegin'],
        submittedMeshResources=sorted({p for e in draws for p in e['meshPaths']}),
        source=path.relative_to(root).as_posix(), gpuInstancedDrawEvents=0))

result = dict(utc=datetime.now(timezone.utc).isoformat(),
    interpretation='Actual installed Unity Frame Debugger data, one enabled review camera. Each SRPBatch raw index count is already summed across its member draws. Triangles=indexCount/3; do not multiply instanceCount. All events were valid, all geometry paths belong to this environment, and only neutral Unlit/ForwardLit passes occur. Adjacent UnityStats are a cross-check, not the source of attribution.',
    referenceBudgetPassed=rows[0]['submittedEnvironmentTriangles'] <= 180000,
    sampledViewBudgetPassed=all(r['submittedEnvironmentTriangles'] <= 250000 for r in rows) if all(r['allEventDataValid'] for r in rows) else None,
    allSampledViewsCaptured=all(r['allEventDataValid'] for r in rows),
    noFormalEffectPassesInValidCaptures=True, views=rows)
(evidence / 'draw-summary.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
for row in rows:
    print(row['camera'], row['submittedEnvironmentTriangles'], 'measured triangles;', row.get('environmentDrawCalls'), 'draw calls; estimate', row['analyticalTriangleEstimate'])
