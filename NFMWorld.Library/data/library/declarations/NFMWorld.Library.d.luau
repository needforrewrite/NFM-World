declare extern type BaseAi with
    runAi: ((self: BaseAi) -> nil)
end

declare BaseAi: {
}

declare extern type AiContext with
    players: { ClientSidePlayer }
    player: ClientSidePlayer
    stage: BackendStage
    config: { [any]: any }|nil
end

declare AiContext: {
}

declare extern type BackendGameObject with
    children: { BackendGameObject }
    parent: BackendGameObject|nil
    position: fixed64vector3
    rotation: f64euler
end

declare BackendGameObject: {
}

declare extern type BackendCar extends BackendGameObject with
    groundAt: integer
    maxRadius: integer
    wheelAngle: f64euler
    turningWheelAngle: f64euler
    wheels: { Rad3dWheelDef }
    carPhysics: CarPhysics
    control: Control
    currentCheckpoint: integer
    currentLap: integer
    totalCheckpoint: integer
    lastCheckpointNode: integer
    placement: integer
    rad: Rad3d
    stats: CarStats
    wasted: boolean
    player: ClientSidePlayerInfo
    children: { BackendGameObject }
    parent: BackendGameObject|nil
    position: fixed64vector3
    rotation: f64euler
    drive: ((self: BackendCar, stage: BackendStage) -> nil)
end

declare BackendCar: {
}

declare extern type BackendStage with
    pieces: { BackendGameObject }
    nodes: { StageObject }
    checkpoints: { StageObject }
    fixHoops: { StageObject }
    nlaps: integer
    name: string
    path: string
    stageLoader: StageLoader
end

declare BackendStage: {
}

declare extern type WallCollision extends BackendGameObject with
    boxes: { Rad3dBoxDef }
    maxRadius: integer
    children: { BackendGameObject }
    parent: BackendGameObject|nil
    position: fixed64vector3
    rotation: f64euler
end

declare WallCollision: {
}

declare extern type StageObject extends BackendGameObject with
    originalPlacement: PiecePlacement
    rad: Rad3d
    nodeKind: AiNodeKind
    isSpecial: boolean
    boxes: { Rad3dBoxDef }
    maxRadius: integer
    fileName: string
    children: { BackendGameObject }
    parent: BackendGameObject|nil
    position: fixed64vector3
    rotation: f64euler
end

declare StageObject: {
}

declare extern type IGamemodeContext with
end

declare IGamemodeContext: {
}

declare extern type LuaClientContext with
    resetCheckpointGlow: ((self: LuaClientContext) -> nil)
    updateCheckpointGlow: ((self: LuaClientContext, currentCheckpoint: integer, isFinish: boolean) -> nil)
    getClientCarCallbacks: ((self: LuaClientContext, car: BackendCar) -> LuaClientCarContext)
end

declare LuaClientContext: {
}

declare extern type LuaClientCarContext with
    castsShadow: boolean
    getsShadowed: boolean|nil
    alphaOverride: number|nil
    glow: boolean|nil
    finish: boolean|nil
end

declare LuaClientCarContext: {
}

declare extern type GamemodeContext with
    stage: BackendStage
    players: { ClientSidePlayer }
    clientPlayer: ClientSidePlayer
    hudState: HudStateData
    physics: PhysicsController
    config: { [any]: any }|nil
    client: LuaClientContext
    countdownInterval: integer
    createCar: ((self: GamemodeContext, playerIndex: integer, x: fixed64, z: fixed64) -> BackendCar)
    calculatePositions: ((self: GamemodeContext) -> nil)
    handleCheckPoint: ((self: GamemodeContext, car: BackendCar) -> boolean)
    handleFixHoops: ((self: GamemodeContext, car: BackendCar) -> boolean)
    clientReset: ((self: GamemodeContext) -> nil)
    sendEvent: ((self: GamemodeContext, type: string, payload: { [any]: any }) -> nil)
    updateHudAndSounds: ((self: GamemodeContext, car: BackendCar) -> nil)
    removeFakePlayers: ((self: GamemodeContext) -> nil)
    clonePlayer: ((self: GamemodeContext, basedOnPlayer: ClientSidePlayer) -> ClientSidePlayer)
end

declare GamemodeContext: {
}

declare extern type ServerGamemodeContext with
    currentStage: BackendStage
    players: { ServerSidePlayerInfo }
    config: { [any]: any }|nil
    countdownInterval: integer
    getPlayerPosition: ((self: ServerGamemodeContext, playerId: string) -> fixed64vector3|nil)
    broadcastEvent: ((self: ServerGamemodeContext, type: string, payload: { [any]: any }) -> nil)
    finishRace: ((self: ServerGamemodeContext, standings: RaceStandings) -> nil)
end

declare ServerGamemodeContext: {
}

declare extern type TimeTrial with
    hasGhost: boolean
    begin: ((self: TimeTrial, car: BackendCar) -> nil)
    applyGhost: ((self: TimeTrial, ghostCar: BackendCar, tick: integer) -> nil)
    record: ((self: TimeTrial, car: BackendCar) -> nil)
    save: ((self: TimeTrial) -> nil)
end

declare TimeTrial: {
    new: ((stage: BackendStage) -> TimeTrial),
}

declare extern type ClientSidePlayer with
    info: ClientSidePlayerInfo
    index: integer
    car: BackendCar|nil
    bot: BaseAi|nil
    isFake: boolean
end

declare ClientSidePlayer: {
}

declare extern type ClientSidePlayerInfo with
    playerName: string
    carName: string
    color: Color3
    isBot: boolean
    isClientPlayer: boolean
end

declare ClientSidePlayerInfo: {
}

declare extern type HudStateData with
    speed: number
    power: number
    damage: number
    lap: integer
    totalLaps: integer
    lapTime: integer
    position: integer
    totalRacers: integer
    stateText: string
    countdownTimer: integer
    stateTextEndsAt: number|nil
end

declare HudStateData: {
}

declare extern type PhysicsController with
    gameTick: ((self: PhysicsController) -> nil)
end

declare PhysicsController: {
}

declare extern type ServerSidePlayerInfo with
    id: string
    playerName: string
    carName: string
    color: Color3
end

declare ServerSidePlayerInfo: {
}

declare extern type AttachmentLineDirection with
end

declare AttachmentLineDirection: {
    x: AttachmentLineDirection,
    z: AttachmentLineDirection,
}

declare extern type LineType with
end

declare LineType: {
    flat: LineType,
    charged: LineType,
    colored: LineType,
    brightColored: LineType,
    maxValue: LineType,
}

declare extern type PolyType with
end

declare PolyType: {
    flat: PolyType,
    light: PolyType,
    brakeLight: PolyType,
    reverseLight: PolyType,
    finish: PolyType,
    fullbright: PolyType,
    cGround: PolyType,
    glass: PolyType,
    maxValue: PolyType,
}

declare extern type Rad3d with
    maxRadius: integer
    colors: { Color3 }
    stats: CarStats
    wheels: { Rad3dWheelDef }
    rims: Rad3dRimsDef|nil
    boxes: { Rad3dBoxDef }
    polys: { Rad3dPoly }
    castsShadow: boolean
    atp: { LuaVector2 }
    fileName: string
    atLines: { Rad3dAttachmentLine }|nil
end

declare Rad3d: {
}

declare extern type Rad3dAttachmentLine with
    direction: AttachmentLineDirection
    offset: fixed64
    __eq: ((left: Rad3dAttachmentLine, right: Rad3dAttachmentLine) -> boolean)
end

declare Rad3dAttachmentLine: {
}

declare extern type Rad3dBoxDef with
    xy: integer
    zy: integer
    radius: fixed64vector3
    translation: fixed64vector3
    surfaceType: SurfaceType
    damage: integer
    notWall: boolean
    color: Color3
    tractionMultiplier: fixed64|nil
    __eq: ((left: Rad3dBoxDef, right: Rad3dBoxDef) -> boolean)
end

declare Rad3dBoxDef: {
}

declare extern type Rad3dPoly with
    color: Color3
    colNum: integer|nil
    polyType: PolyType
    lineType: LineType|nil
    __eq: ((left: Rad3dPoly, right: Rad3dPoly) -> boolean)
end

declare Rad3dPoly: {
}

declare extern type Rad3dRimsDef with
    color: Color3
    size: number
    depth: number
    __eq: ((left: Rad3dRimsDef, right: Rad3dRimsDef) -> boolean)
end

declare Rad3dRimsDef: {
}

declare extern type Rad3dWheelDef with
    position: fixed64vector3
    rotates: integer
    width: fixed64
    height: fixed64
    __eq: ((left: Rad3dWheelDef, right: Rad3dWheelDef) -> boolean)
end

declare Rad3dWheelDef: {
}

declare extern type SurfaceType with
end

declare SurfaceType: {
    road: SurfaceType,
    offTrack: SurfaceType,
    offRoad: SurfaceType,
    bump: SurfaceType,
    bumpySides: SurfaceType,
    spikes: SurfaceType,
}

declare extern type CarPhysics with
    halted: boolean
    btab: boolean
    capcnt: integer
    capsized: boolean
    caught: { boolean }
    stat: CarStats
    cn: integer
    cntdest: integer
    cntouch: integer
    collidingWithClientPlayer: boolean
    crank: { integer }
    lcrank: { integer }
    cxz: fixed64
    staticCameraXz: fixed64
    dcnt: integer
    dcomp: fixed64
    lcomp: fixed64
    wasted: boolean
    dominate: { boolean }
    drag: fixed64
    fixes: integer
    forca: fixed64
    ftab: boolean
    turnXz: fixed64
    gtouch: boolean
    hitmag: integer
    im: integer
    lastcolido: integer
    loop: integer
    lxz: fixed64
    mtouch: boolean
    mxz: fixed64
    numRoofDamage: integer
    newcar: boolean
    newedcar: integer
    nmlt: integer
    nofocus: boolean
    outshakedam: integer
    pd: boolean
    pl: boolean
    pmlt: integer
    point: integer
    power: fixed64
    powerup: fixed64
    pr: boolean
    pu: boolean
    pushed: boolean
    pxy: fixed64
    pzy: fixed64
    rcomp: fixed64
    rtab: boolean
    scx: { fixed64 }
    scy: { fixed64 }
    scz: { fixed64 }
    shakedam: integer
    skid: integer
    speed: fixed64
    roofDamage: integer
    surfCount: integer
    surfing: boolean
    tilt: fixed64
    totalStuntXy: fixed64
    totalStuntXz: fixed64
    totalStuntZy: fixed64
    tcnt: integer
    txz: fixed64
    ucomp: fixed64
    wtouch: boolean
    xtpower: integer
end

declare CarPhysics: {
}

declare extern type CarStats with
    swits: Int3
    acelf: fixed64vector3
    handb: integer
    airs: fixed64
    airc: integer
    grip: fixed64
    bounce: fixed64
    simag: fixed64
    moment: fixed64
    comprad: fixed64
    push: fixed64
    revpush: fixed64
    lift: integer
    revlift: integer
    powerloss: integer
    flipy: integer
    msquash: integer
    clrad: integer
    dammult: fixed64
    maxmag: integer
    dishandle: fixed64
    outdam: fixed64
    name: string
    enginsignature: integer
    turnradius: integer
    roadgrip: fixed64|nil
    offroadgrip: fixed64|nil
    offtrackgrip: fixed64|nil
    turn: fixed64
    __eq: ((left: CarStats, right: CarStats) -> boolean)
end

declare CarStats: {
}

declare extern type Int3 with
    x: integer
    y: integer
    z: integer
    __eq: ((left: Int3, right: Int3) -> boolean)
end

declare Int3: {
}

declare extern type Collection with
end

declare Collection: {
    nFMM: Collection,
    nFMMUser: Collection,
    world: Collection,
    worldUser: Collection,
    elo: Collection,
    user: Collection,
    football: Collection,
}

declare extern type Control with
    arrace: boolean
    chatup: integer
    down: boolean
    enter: boolean
    exit: boolean
    handb: boolean
    multion: integer
    mutem: boolean
    mutes: boolean
    radar: boolean
    right: boolean
    up: boolean
    left: boolean
    lookback: integer
    wall: integer
    zyinv: boolean
end

declare Control: {
}

declare extern type AiNodeKind with
end

declare AiNodeKind: {
    checkPoint: AiNodeKind,
    road: AiNodeKind,
    turn: AiNodeKind,
    auto: AiNodeKind,
    ramp: AiNodeKind,
    halfpipe: AiNodeKind,
    sequenceStart: AiNodeKind,
    sequenceEnd: AiNodeKind,
    fixRoadStart: AiNodeKind,
    fixRamp: AiNodeKind,
    fixHoop: AiNodeKind,
    fixRoadEnd: AiNodeKind,
    avoid: AiNodeKind,
    reset: AiNodeKind,
}

declare extern type PiecePlacement with
    type: PiecePlacementType
    object: Rad3d
    position: fixed64vector3
    rotation: f64euler
    nodeKind: AiNodeKind|nil
    isSpecial: boolean
    isWall: boolean
    __eq: ((left: PiecePlacement, right: PiecePlacement) -> boolean)
end

declare PiecePlacement: {
}

declare extern type PiecePlacementType with
end

declare PiecePlacementType: {
    collisionObject: PiecePlacementType,
    checkPoint: PiecePlacementType,
    fixHoop: PiecePlacementType,
}

declare extern type StageWall with
    direction: WallDirection
    count: integer
    position: integer
    offset: integer
    __eq: ((left: StageWall|nil, right: StageWall|nil) -> boolean)
end

declare StageWall: {
}

declare extern type WallDirection with
end

declare WallDirection: {
    right: WallDirection,
    left: WallDirection,
    top: WallDirection,
    bottom: WallDirection,
}

declare extern type HierarchyGroup with
    name: string
    pieces: { PiecePlacement }
    coordinateKeys: { string }
    __eq: ((left: HierarchyGroup, right: HierarchyGroup) -> boolean)
end

declare HierarchyGroup: {
}

declare extern type EnvironmentInstruction with
    __eq: ((left: EnvironmentInstruction|nil, right: EnvironmentInstruction|nil) -> boolean)
end

declare EnvironmentInstruction: {
}

declare extern type SnapInstruction extends EnvironmentInstruction with
    color: Color3
    __eq: ((left: SnapInstruction|nil, right: SnapInstruction|nil) -> boolean)
end

declare SnapInstruction: {
}

declare extern type SkyInstruction extends EnvironmentInstruction with
    color: Color3
    __eq: ((left: SkyInstruction|nil, right: SkyInstruction|nil) -> boolean)
end

declare SkyInstruction: {
}

declare extern type FogInstruction extends EnvironmentInstruction with
    color: Color3
    __eq: ((left: FogInstruction|nil, right: FogInstruction|nil) -> boolean)
end

declare FogInstruction: {
}

declare extern type CloudsInstruction extends EnvironmentInstruction with
    clouds: { integer }
    __eq: ((left: CloudsInstruction|nil, right: CloudsInstruction|nil) -> boolean)
end

declare CloudsInstruction: {
}

declare extern type GroundInstruction extends EnvironmentInstruction with
    color: Color3
    __eq: ((left: GroundInstruction|nil, right: GroundInstruction|nil) -> boolean)
end

declare GroundInstruction: {
}

declare extern type TextureInstruction extends EnvironmentInstruction with
    texture: { integer }
    __eq: ((left: TextureInstruction|nil, right: TextureInstruction|nil) -> boolean)
end

declare TextureInstruction: {
}

declare extern type PolysInstruction extends EnvironmentInstruction with
    color: Color3
    __eq: ((left: PolysInstruction|nil, right: PolysInstruction|nil) -> boolean)
end

declare PolysInstruction: {
}

declare extern type StageLoader with
    path: string
    nlaps: integer
    musicPath: string
    remasteredMusicPath: string
    musicFreqMul: number
    musicTempoMul: number
    name: string
    indexOffset: integer
    sx: integer
    sz: integer
    ncx: integer
    ncz: integer
    cloudCoverage: number|nil
    fogDensity: integer|nil
    fadeFrom: integer|nil
    lightsOn: boolean
    drawMountains: boolean
    mountainSeed: integer|nil
    mountainCoverage: number|nil
    lightDirection: LuaVector3|nil
    pieces: { PiecePlacement }
    walls: { Rad3dBoxDef }
    maxr: integer
    maxl: integer
    maxt: integer
    maxb: integer
    environmentInstructions: { EnvironmentInstruction }
    drawPolys: boolean
    drawClouds: boolean
end

declare StageLoader: {
}

declare extern type Color3 with
    r: integer
    g: integer
    b: integer
    __add: ((a: Color3, b: Color3) -> Color3)
    __div: ((a: Color3, b: number) -> Color3)
    __sub: ((a: Color3, b: Color3) -> Color3)
    __eq: ((left: Color3, right: Color3) -> boolean)
end

declare Color3: {
}

declare extern type FrameTrace with
end

declare FrameTrace: {
    addMessage: ((message: string) -> nil),
}

