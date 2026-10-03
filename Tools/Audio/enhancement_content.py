"""Original bilingual script, fixed casting, no runtime cloud dependency."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ROLES = {
    'anchor': ('主播 / CIVIL NETWORK', 'en_female_dacey_uranus_bigtts'),
    'commander': ('指挥官 / FLEET COMMAND', 'en_male_tim_uranus_bigtts'),
    'engineer': ('工程师 / SCIENCE CHANNEL', 'en_female_jane_uranus_bigtts'),
    'comms': ('通信军官 / CONTACT CONTROL', 'en_female_stokie_uranus_bigtts'),
}

# Ten exchanges per act; each conveys new information during uninterrupted approach.
STORY = [
('anchor', 'Tonight, every public screen carries the same picture: a silver point, crossing the outer watch line without a sound.', '今夜，所有公共屏幕都显示着同一幅画面：一个银色光点，无声地越过外层警戒线。'),
('anchor', 'The markets have named it an opportunity. The ministries call it an incident. Children watching from the night side call it a star.', '市场把它称作机遇，各部称它为事件；在夜半球观看的孩子们，把它叫作星星。'),
('anchor', 'We have learned to build cities between planets, and still we ask a stranger the oldest question: what can you give us?', '我们已学会在行星之间建造城市，却仍向陌生者提出最古老的问题：你能给我们什么？'),
('comms', 'Civil network, this is contact control. Your picture is delayed. Please remind your audience that the object has not acknowledged us.', '民用频道，这里是接触管制。你们的画面存在延迟。请提醒观众：目标尚未回应我们。'),
('anchor', 'That silence has become whatever people need it to be. A promise, a threat, a proof that their rivals were wrong.', '人们把那片沉默解释成自己需要的模样：承诺、威胁，或是对手犯错的证明。'),
('engineer', 'For the record, silence is only silence. We have no exhaust spectrum, no readable markings, and no measurement of its interior.', '记录在案：沉默只是沉默。我们没有尾气光谱、可辨识标记，也没有内部结构测量。'),
('anchor', 'The assembly has voted to receive a delegation. There is, as yet, no evidence that a delegation is coming.', '议会已表决同意接待代表团。但截至目前，没有任何证据表明代表团正在到来。'),
('commander', 'Our crews did not choose the headlines. They trained to keep a corridor open, and that is what they will do.', '我们的船员并不决定新闻标题。他们接受训练，为的是守住一条通道。他们会履行职责。'),
('anchor', 'Two thousand hulls are holding that corridor. Behind each bright engine is a person who expected to come home after this shift.', '两千艘舰船正守着那条通道。每台明亮引擎后，都有一个原本打算下班回家的人。'),
('comms', 'Transferring the public relay to the fleet approach channel. We are keeping the emergency band clear. Contact continues inbound.', '公共转播转接舰队接近频道。紧急频段保持畅通。接触目标仍在向内飞行。'),

('comms', 'Unidentified craft, this is the boundary fleet. You are entering a controlled transit lane. Reduce speed and identify your point of origin.', '不明飞行器，这里是边界舰队。你正在进入受控航道。请减速并报告出发地点。'),
('commander', 'Hold formation. Keep the approach corridor open. No captain is to mistake a bright reflection for a weapon discharge.', '保持阵型，留出接近走廊。任何舰长都不得把明亮反光误判为武器发射。'),
('comms', 'We can provide navigation references and a protected holding position. Acknowledge on this carrier, or change your course to confirm reception.', '我们可以提供导航参照与安全停泊点。请在本载波回应，或改变航向以确认收到信息。'),
('engineer', 'Its track is exceptionally clean. Our ranging pulses return without the scattering we expect from seams, antennae, or exposed machinery.', '它的轨迹异常干净。测距脉冲的回波中，没有接缝、天线或外露机械通常产生的散射。'),
('comms', 'Still no reply. I have repeated the invitation in the contact protocol, mathematics, and a plain sequence of navigation lights.', '仍无回应。我已用接触协议、数学编码和简单导航灯序列发送邀请。'),
('commander', 'Then transmit the terms. Stop outside the inner line, power down any weapon system, and accept remote inspection. No boarding party will approach.', '那就发送条件：停在内侧警戒线外，关闭武器系统，接受远程检查。不会有登舰队靠近。'),
('anchor', 'The word surrender is already spreading through the public feeds. Fleet command has issued instructions; the visitor has made no agreement.', '“投降”一词已在公共信息流中传播。舰队司令部发出了指令，来客却没有作出任何承诺。'),
('engineer', 'Commander, our instruments cannot establish that it has a power system to shut down. We are describing our ships, not necessarily theirs.', '指挥官，仪器甚至无法确认它是否有可关闭的动力系统。我们描述的是自己的舰船，未必是它。'),
('commander', 'Understood. Keep recording. If it can receive any part of our message, it will know there is a safe way to stop.', '明白，继续记录。只要它能接收部分信息，就会知道这里提供了安全停下的方式。'),
('comms', 'The point is becoming a shape now. Rounded nose, narrow tail. No visible attitude jets. Approach speed has not changed.', '那个光点开始显出轮廓：圆头、细尾，没有可见姿态喷口。接近速度没有改变。'),

('engineer', 'I need the weapons crews to hear this. A polished surface is not a damage assessment, and a small silhouette is not a measure of strength.', '我需要武器操作员听清楚：光滑表面不是损伤评估，小小的轮廓也不代表强度低。'),
('commander', 'We have crossed weapons to cover the corridor without closing it. The object cannot threaten every hull at once. That is our working assumption.', '我们以交叉火力覆盖走廊，但没有封死通道。目标无法同时威胁所有舰体，这是当前判断。'),
('engineer', 'Then mark that sentence as an assumption. Its surface temperature has barely changed under our ranging pulses. We do not know where the energy goes.', '那就请把它明确标为假设。测距脉冲照射后，它的表面温度几乎未变。我们不知道能量去了哪里。'),
('comms', 'Science channel is requesting wider separation between ships. Command channel is requesting a tighter firing solution. Both orders are waiting for confirmation.', '科学频道请求增大舰间距；指挥频道要求缩紧射击解算。两条指令都在等待确认。'),
('commander', 'Maintain spacing for now. A sudden withdrawal could look like preparation to fire. I will not start a war by moving a symbol on a display.', '暂时保持间距。突然后撤可能被理解为开火准备。我不能因为屏幕上的一个符号而发动战争。'),
('engineer', 'And I will not call a formation safe because its symbols are tidy. Give the outer ranks independent escape vectors before anything happens.', '我也不会因为符号排列整齐，就说阵型安全。请在事发前，给外层各列分配独立撤离方向。'),
('commander', 'Approved. Load evacuation routes, but hold your position until threatened. Damaged ships get priority on rescue traffic. Keep those routes out of the firing lanes.', '批准。载入撤离路线，受威胁前保持位置。受损舰优先使用救援频道，撤离路线避开射界。'),
('comms', 'Routes distributed. Captains confirm local control and independent engines. The emergency channel will identify each hull, even if the formation breaks apart.', '路线已分发。各舰长确认本地控制与独立引擎可用。即使阵型解体，紧急频道仍将识别每艘舰。'),
('anchor', 'For those listening at home, disagreement is not panic. It is what careful people do when the picture is clearer than the explanation.', '致家中的听众：分歧并不等于恐慌。画面清晰而解释不足时，谨慎的人就应当讨论。'),
('engineer', 'One last recommendation: if you see a beam return, clear its path immediately. Do not assume the return will follow the line back to its source.', '最后一条建议：一旦看到光束折返，立即避开其路径。不要假定反射光会沿原路回到发射源。'),

('comms', 'Unidentified craft, this is your final warning. You are approaching the inner safety line. Alter course now. We are leaving the corridor ahead of you open.', '不明飞行器，这是最后警告。你即将接近内侧安全线。立即改变航向；你前方的通道仍然开放。'),
('commander', 'All batteries, track only. Confirm the hull behind your target before enabling a beam. The background on your screen is another crew.', '所有炮组，仅跟踪。启动光束前确认目标后方的舰体。屏幕里的背景，是另一群船员。'),
('engineer', 'The latest return resolves a continuous surface. No opening, no joint, no obvious heat rejection. Please keep every sensor recording through first contact.', '最新回波显示连续表面：无开口、无接缝，也无明显散热。请让每台传感器持续记录首次接触。'),
('comms', 'No acknowledgement. Carrier level is steady. This is not a lost connection; there is simply nobody answering on the frequencies we understand.', '没有确认。载波强度稳定。这并非连接丢失，只是在我们理解的频率上，始终无人回应。'),
('commander', 'If the object crosses the line, the nearest batteries may fire controlled bursts. Break off if the beam is reflected. Do not chase a bad shot.', '如果目标越线，最近的炮组可以短促射击。出现反射立即停火，不能追着错误的射线继续打。'),
('engineer', 'If a hull is pierced, isolate its reactor and keep clear. A quiet wreck can still be counting down. Rescue crews must wait for a safe approach.', '舰体若被贯穿，立即隔离反应堆并保持距离。安静的残舰仍可能在倒计时。救援组必须等待安全接近。'),
('anchor', 'The civilian relay will remain open as long as contact control can carry it. There will be no victory announcement written in advance tonight.', '只要接触管制还能维持转播，民用频道就会保持开放。今夜，不会有提前写好的胜利公告。'),
('comms', 'Inner line crossing confirmed. I have two thousand identification signals, all distinct. Emergency band is live. Captains, answer with your own hull call sign.', '确认越过内侧警戒线。两千个识别信号，各自独立。紧急频段已开放。各舰长，请用本舰呼号回应。'),
('commander', 'Weapons free within your assigned lanes. Keep your escape route clear. If your neighbor is hit, move first and report when you can.', '允许在指定射界内开火。保持撤离路线畅通。邻舰中弹时先移动，能够通信后再报告。'),
('engineer', 'It is still coming. Watch the contact point, not the glare. Whatever happens next, trust the measurements. All stations, protect your people.', '它仍在前进。看接触点，不要被眩光迷惑。无论接下来发生什么，都要相信测量。所有岗位，保护好你们的人。'),
]

COMBAT = {
 'AttackIneffective': [('engineer','No thermal rise. That shot did nothing.','温度没有升高。这一击毫无作用。'),('commander','Cease wasteful fire. Check the return path.','停止无效射击，检查反射路径。'),('engineer','Surface intact. I cannot find a mark.','表面完好，找不到任何痕迹。'),('comms','All batteries, your beams are scattering across our lanes.','各炮组，光束正在散入我方航道。')],
 'LaserReflected': [('comms','Reflected beam! Clear that line now!','反射光束！立即让开那条线！'),('engineer','The angle changed. The return is crossing the formation!','角度变了，反射束正在横穿阵型！'),('commander','Hold fire! Friendly hull behind the reflection!','停火！反射方向后方有友舰！'),('engineer','Contact confirmed. Energy is leaving the surface, not entering it.','确认接触。能量离开了表面，并未进入。')],
 'HullPenetrated': [('engineer','Breach through both sides. Isolating the reactor!','两侧贯穿。正在隔离反应堆！'),('comms','We have lost pressure across the central decks!','中央甲板全面失压！'),('engineer','It went straight through us. Close every internal door!','它直接穿过了我们，关闭全部内部舱门！'),('comms','Hull pierced. Navigation is gone. We are drifting!','舰体被贯穿，导航失效，我们正在漂移！')],
 'ReactorUnstable': [('engineer','Containment is failing. Everyone away from the core!','约束正在失效，所有人远离核心！'),('engineer','The shutdown did not take. Reactor temperature is still climbing!','停堆没有生效，反应堆温度仍在上升！'),('comms','Reactor warning. Do not bring a rescue ship alongside us!','反应堆警报！救援舰不要靠近我们！'),('engineer','We are out of cooling. There is no restart from this!','冷却已经耗尽，再也无法重启了！')],
 'RescueRequested': [('comms','Mayday. We have survivors in the forward section. Can anyone hear us?','求救！前舱仍有幸存者，有人能听到吗？'),('comms','Rescue channel, our launch bay is sealed. We need an external pickup!','救援频道，我们的机库封死了，需要外部接应！'),('engineer','We are getting them into suits. Please keep a receiver on us.','正在帮他们穿上宇航服，请保持监听。'),('comms','Emergency beacon active. We cannot control our drift. Please respond!','紧急信标已启动，漂移无法控制，请回应！')],
 'ShipExploded': [('comms','We lost that hull. Rescue craft, stand clear of the debris.','那艘舰消失了。救援艇远离残骸。'),('commander','Explosion confirmed. Open a lane for the survivors.','确认爆炸，为幸存者让出通道。'),('engineer','Secondary flash. Watch for another reactor failure nearby.','出现次级闪光，警惕附近再次失稳。'),('comms','Their signal is gone. I am marking the last known position.','他们的信号断了，正在标记最后位置。')],
 'RetreatOrdered': [('commander','Break formation. Accelerate along your assigned escape vector!','脱离阵型，沿分配的撤离方向加速！'),('comms','Local retreat confirmed. Keep clear of the damaged ships.','确认本地撤退，避开受损舰。'),('commander','Do not wait for the whole fleet. Move your own ship now!','别再等待全舰队，现在就移动本舰！'),('engineer','Engines responding. We are opening the distance.','引擎有响应，我们正在拉开距离。')],
 'ShipEscaped': [('comms','One hull has cleared the combat boundary. Beacon still active.','一艘舰已离开战斗边界，信标仍然工作。'),('commander','Escape confirmed. Keep moving until you reach the assembly area.','确认逃脱，继续前往集结区域。'),('comms','That crew is outside the engagement zone. Sending their new coordinates.','那群船员已经离开交战区，正在发送新坐标。'),('engineer','Their drive signature is stable beyond the perimeter.','边界外，他们的推进信号已经稳定。')],
 'CommunicationInterrupted': [('comms','Transmission cut off. Switching to the next live beacon.','通信被切断，切换下一个有效信标。'),('comms','No carrier from that ship. Keep the emergency band clear.','那艘舰的载波消失了，保持紧急频段畅通。'),('commander','We heard the distress call. Mark the position and continue withdrawal.','我们听到了求救，标记位置，继续撤离。'),('engineer','Their telemetry stopped with the flash. I have no further readings.','闪光时遥测也停止了，没有后续读数。')],
}

def content():
    result={'title':'The Open Corridor','voiceProvenance':'AI synthesized English / Seed-TTS 2.0; original fictional dialogue', 'roles':[], 'narrative':[], 'combat':[]}
    for role,(channel,speaker) in ROLES.items(): result['roles'].append({'role':role,'channel':channel,'speaker':speaker})
    for i,(role,en,zh) in enumerate(STORY):
        result['narrative'].append(dict(id=f'E{i+1:03}',role=role,channel=ROLES[role][0],text=zh,english=en,stage=i//10,cameraShot=i//10,direction='calm' if i<20 else 'tense',duration=0,time=0))
    for kind,rows in COMBAT.items():
        for role,en,zh in rows:
            result['combat'].append(dict(id=f'B{len(result["combat"])+1:03}',role=role,channel=ROLES[role][0],eventKind=kind,text=zh,english=en,direction='urgent',pendingOnly=kind in ['HullPenetrated','ReactorUnstable','RescueRequested'],weight=1,important=kind in ['LaserReflected','ReactorUnstable','RetreatOrdered']))
    return result

if __name__=='__main__':
    target=ROOT/'Tools/Audio/EnhancementEnglish.json'
    target.write_text(json.dumps(content(),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    c=content();print(f'{len(c["narrative"])} opening lines, {len(c["combat"])} event lines, {sum(len(x["english"].split()) for x in c["narrative"])} opening words')
