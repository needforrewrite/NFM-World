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

declare extern type FrameTrace with
end

declare FrameTrace: {
    addMessage: ((message: string) -> nil),
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
    lapDiffMs: integer|nil
    lastLapDiffMs: integer|nil
    chkDiffMs: integer|nil
    lastChkDiffMs: integer|nil
    countdownTimer: integer
    stateTextEndsAt: number|nil
end

declare HudStateData: {
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
    getSplitDiff: ((self: TimeTrial, splitIndex: integer) -> number|nil)
    getLastSplitDiff: ((self: TimeTrial) -> number|nil)
    getLapDiff: ((self: TimeTrial, lapIndex: integer) -> number|nil)
    recordSplit: ((self: TimeTrial, splitTime: number) -> nil)
    getLapTime: ((self: TimeTrial, lapIndex: integer) -> number|nil)
    getLastSplitTime: ((self: TimeTrial) -> number|nil)
    getBestLastSplitTime: ((self: TimeTrial) -> number|nil)
    record: ((self: TimeTrial, car: BackendCar) -> nil)
    save: ((self: TimeTrial) -> nil)
end

declare TimeTrial: {
    new: ((stage: BackendStage) -> TimeTrial),
}

declare extern type PhysicsController with
    gameTick: ((self: PhysicsController) -> nil)
end

declare PhysicsController: {
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

declare extern type Key with
end

declare Key: {
    keyCode: Key,
    modifiers: Key,
    none: Key,
    lButton: Key,
    rButton: Key,
    cancel: Key,
    mButton: Key,
    xButton1: Key,
    xButton2: Key,
    back: Key,
    tab: Key,
    lineFeed: Key,
    clear: Key,
    returnKey: Key,
    enter: Key,
    shiftKey: Key,
    controlKey: Key,
    menu: Key,
    pause: Key,
    capital: Key,
    capsLock: Key,
    kanaMode: Key,
    hanguelMode: Key,
    hangulMode: Key,
    junjaMode: Key,
    finalMode: Key,
    hanjaMode: Key,
    kanjiMode: Key,
    escape: Key,
    iMEConvert: Key,
    iMENonconvert: Key,
    iMEAccept: Key,
    iMEAceept: Key,
    iMEModeChange: Key,
    space: Key,
    prior: Key,
    pageUp: Key,
    next: Key,
    pageDown: Key,
    endKey: Key,
    home: Key,
    left: Key,
    up: Key,
    right: Key,
    down: Key,
    select: Key,
    print: Key,
    execute: Key,
    snapshot: Key,
    printScreen: Key,
    insert: Key,
    delete: Key,
    help: Key,
    d0: Key,
    d1: Key,
    d2: Key,
    d3: Key,
    d4: Key,
    d5: Key,
    d6: Key,
    d7: Key,
    d8: Key,
    d9: Key,
    a: Key,
    b: Key,
    c: Key,
    d: Key,
    e: Key,
    f: Key,
    g: Key,
    h: Key,
    i: Key,
    j: Key,
    k: Key,
    l: Key,
    m: Key,
    n: Key,
    o: Key,
    p: Key,
    q: Key,
    r: Key,
    s: Key,
    t: Key,
    u: Key,
    v: Key,
    w: Key,
    x: Key,
    y: Key,
    z: Key,
    lWin: Key,
    rWin: Key,
    apps: Key,
    sleep: Key,
    numPad0: Key,
    numPad1: Key,
    numPad2: Key,
    numPad3: Key,
    numPad4: Key,
    numPad5: Key,
    numPad6: Key,
    numPad7: Key,
    numPad8: Key,
    numPad9: Key,
    multiply: Key,
    add: Key,
    separator: Key,
    subtract: Key,
    decimal: Key,
    divide: Key,
    f1: Key,
    f2: Key,
    f3: Key,
    f4: Key,
    f5: Key,
    f6: Key,
    f7: Key,
    f8: Key,
    f9: Key,
    f10: Key,
    f11: Key,
    f12: Key,
    f13: Key,
    f14: Key,
    f15: Key,
    f16: Key,
    f17: Key,
    f18: Key,
    f19: Key,
    f20: Key,
    f21: Key,
    f22: Key,
    f23: Key,
    f24: Key,
    numLock: Key,
    scroll: Key,
    lShiftKey: Key,
    rShiftKey: Key,
    lControlKey: Key,
    rControlKey: Key,
    lMenu: Key,
    rMenu: Key,
    browserBack: Key,
    browserForward: Key,
    browserRefresh: Key,
    browserStop: Key,
    browserSearch: Key,
    browserFavorites: Key,
    browserHome: Key,
    volumeMute: Key,
    volumeDown: Key,
    volumeUp: Key,
    mediaNextTrack: Key,
    mediaPreviousTrack: Key,
    mediaStop: Key,
    mediaPlayPause: Key,
    launchMail: Key,
    selectMedia: Key,
    launchApplication1: Key,
    launchApplication2: Key,
    oemSemicolon: Key,
    oem1: Key,
    oemplus: Key,
    oemcomma: Key,
    oemMinus: Key,
    oemPeriod: Key,
    oemQuestion: Key,
    oem2: Key,
    oem3: Key,
    oemtilde: Key,
    oemOpenBrackets: Key,
    oem4: Key,
    oemPipe: Key,
    oem5: Key,
    oemCloseBrackets: Key,
    oem6: Key,
    oem7: Key,
    oemQuotes: Key,
    oem8: Key,
    oem102: Key,
    oemBackslash: Key,
    processKey: Key,
    packet: Key,
    attn: Key,
    crsel: Key,
    exsel: Key,
    eraseEof: Key,
    play: Key,
    zoom: Key,
    noName: Key,
    pa1: Key,
    oemClear: Key,
    shift: Key,
    control: Key,
    alt: Key,
}

declare extern type Keys with
    none: boolean
    lButton: boolean
    rButton: boolean
    cancel: boolean
    mButton: boolean
    xButton1: boolean
    xButton2: boolean
    back: boolean
    tab: boolean
    lineFeed: boolean
    clear: boolean
    returnKey: boolean
    enter: boolean
    shiftKey: boolean
    controlKey: boolean
    menu: boolean
    pause: boolean
    capital: boolean
    capsLock: boolean
    kanaMode: boolean
    hanguelMode: boolean
    hangulMode: boolean
    junjaMode: boolean
    finalMode: boolean
    hanjaMode: boolean
    kanjiMode: boolean
    escape: boolean
    iMEConvert: boolean
    iMENonconvert: boolean
    iMEAccept: boolean
    iMEAceept: boolean
    iMEModeChange: boolean
    space: boolean
    prior: boolean
    pageUp: boolean
    next: boolean
    pageDown: boolean
    endKey: boolean
    home: boolean
    left: boolean
    up: boolean
    right: boolean
    down: boolean
    select: boolean
    print: boolean
    execute: boolean
    snapshot: boolean
    printScreen: boolean
    insert: boolean
    delete: boolean
    help: boolean
    d0: boolean
    d1: boolean
    d2: boolean
    d3: boolean
    d4: boolean
    d5: boolean
    d6: boolean
    d7: boolean
    d8: boolean
    d9: boolean
    a: boolean
    b: boolean
    c: boolean
    d: boolean
    e: boolean
    f: boolean
    g: boolean
    h: boolean
    i: boolean
    j: boolean
    k: boolean
    l: boolean
    m: boolean
    n: boolean
    o: boolean
    p: boolean
    q: boolean
    r: boolean
    s: boolean
    t: boolean
    u: boolean
    v: boolean
    w: boolean
    x: boolean
    y: boolean
    z: boolean
    lWin: boolean
    rWin: boolean
    apps: boolean
    sleep: boolean
    numPad0: boolean
    numPad1: boolean
    numPad2: boolean
    numPad3: boolean
    numPad4: boolean
    numPad5: boolean
    numPad6: boolean
    numPad7: boolean
    numPad8: boolean
    numPad9: boolean
    multiply: boolean
    add: boolean
    separator: boolean
    subtract: boolean
    decimal: boolean
    divide: boolean
    f1: boolean
    f2: boolean
    f3: boolean
    f4: boolean
    f5: boolean
    f6: boolean
    f7: boolean
    f8: boolean
    f9: boolean
    f10: boolean
    f11: boolean
    f12: boolean
    f13: boolean
    f14: boolean
    f15: boolean
    f16: boolean
    f17: boolean
    f18: boolean
    f19: boolean
    f20: boolean
    f21: boolean
    f22: boolean
    f23: boolean
    f24: boolean
    numLock: boolean
    scroll: boolean
    lShiftKey: boolean
    rShiftKey: boolean
    lControlKey: boolean
    rControlKey: boolean
    lMenu: boolean
    rMenu: boolean
    browserBack: boolean
    browserForward: boolean
    browserRefresh: boolean
    browserStop: boolean
    browserSearch: boolean
    browserFavorites: boolean
    browserHome: boolean
    volumeMute: boolean
    volumeDown: boolean
    volumeUp: boolean
    mediaNextTrack: boolean
    mediaPreviousTrack: boolean
    mediaStop: boolean
    mediaPlayPause: boolean
    launchMail: boolean
    selectMedia: boolean
    launchApplication1: boolean
    launchApplication2: boolean
    oemSemicolon: boolean
    oem1: boolean
    oemplus: boolean
    oemcomma: boolean
    oemMinus: boolean
    oemPeriod: boolean
    oemQuestion: boolean
    oem2: boolean
    oem3: boolean
    oemtilde: boolean
    oemOpenBrackets: boolean
    oem4: boolean
    oemPipe: boolean
    oem5: boolean
    oemCloseBrackets: boolean
    oem6: boolean
    oem7: boolean
    oemQuotes: boolean
    oem8: boolean
    oem102: boolean
    oemBackslash: boolean
    processKey: boolean
    packet: boolean
    attn: boolean
    crsel: boolean
    exsel: boolean
    eraseEof: boolean
    play: boolean
    zoom: boolean
    noName: boolean
    pa1: boolean
    oemClear: boolean
    __eq: ((left: Keys, right: Keys) -> boolean)
    __lt: ((left: Keys, right: Keys) -> boolean)
    __le: ((left: Keys, right: Keys) -> boolean)
end

declare Keys: {
}

declare extern type MouseButton with
end

declare MouseButton: {
    primary: MouseButton,
    secondary: MouseButton,
    middle: MouseButton,
    xButton1: MouseButton,
    xButton2: MouseButton,
}

declare extern type MouseButtons with
end

declare MouseButtons: {
    none: MouseButtons,
    primary: MouseButtons,
    secondary: MouseButtons,
    middle: MouseButtons,
    xButton1: MouseButtons,
    xButton2: MouseButtons,
}

declare extern type LuaRect with
    x: number
    y: number
    width: number
    height: number
    __eq: ((left: LuaRect, right: LuaRect) -> boolean)
end

declare LuaRect: {
}

declare extern type LuaVector2 with
    x: number
    y: number
    __sub: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __add: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __div: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __eq: ((left: LuaVector2, right: LuaVector2) -> boolean)
end

declare LuaVector2: {
}

declare extern type LuaVector3 with
    x: number
    y: number
    z: number
    __sub: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __add: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __div: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __eq: ((left: LuaVector3, right: LuaVector3) -> boolean)
end

declare LuaVector3: {
}

declare extern type DeterministicRandom with
    next: ((self: DeterministicRandom) -> integer)
    nextBetween: ((self: DeterministicRandom, min: integer, max: integer) -> integer)
    nextf64: ((self: DeterministicRandom) -> fixed64)
end

declare DeterministicRandom: {
    new: ((value: fixed64) -> DeterministicRandom),
}

declare extern type Stopwatch with
    isRunning: boolean
    elapsed: number
    elapsedMilliseconds: integer
    elapsedMicroseconds: integer
    stop: ((self: Stopwatch) -> nil)
    start: ((self: Stopwatch) -> nil)
    restart: ((self: Stopwatch) -> nil)
    reset: ((self: Stopwatch) -> nil)
end

declare Stopwatch: {
    new: (() -> Stopwatch),
    startNew: (() -> Stopwatch),
}

declare extern type Node with
    visualParent: Node|nil
    visualChildren: { Node }
end

declare Node: {
}

declare extern type Component extends Node with
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: Component, child: Node) -> nil)
    insertAt: ((self: Component, index: integer, child: Node) -> nil)
    removeAt: ((self: Component, index: integer) -> nil)
    scrollIntoView: ((self: Component) -> nil)
    focus: ((self: Component) -> nil)
    blur: ((self: Component) -> nil)
end

declare Component: {
}

declare extern type TextInput extends Component with
    placeholder: string
    text: string
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: TextInput, child: Node) -> nil)
    insertAt: ((self: TextInput, index: integer, child: Node) -> nil)
    removeAt: ((self: TextInput, index: integer) -> nil)
    scrollIntoView: ((self: TextInput) -> nil)
    focus: ((self: TextInput) -> nil)
    blur: ((self: TextInput) -> nil)
end

declare TextInput: {
}

declare extern type View extends Component with
    visualChildren: { Node }
    canHaveChildren: boolean
    name: string
    isFocusable: boolean
    layoutMarginPosition: LuaVector2
    layoutMarginSize: LuaVector2
    layoutBorderPosition: LuaVector2
    layoutBorderSize: LuaVector2
    layoutPaddingPosition: LuaVector2
    layoutPaddingSize: LuaVector2
    layoutContentPosition: LuaVector2
    layoutContentSize: LuaVector2
    layoutMargin: LuaVector2
    layoutPadding: LuaVector2
    layoutBorder: LuaVector2
    scissorRect: LuaRect
    layoutWidth: number
    layoutHeight: number
    layoutX: number
    layoutY: number
    layoutDirection: Direction
    hadOverflow: boolean
    layoutMarginTop: number
    layoutMarginBottom: number
    layoutMarginLeft: number
    layoutMarginRight: number
    layoutPaddingTop: number
    layoutPaddingBottom: number
    layoutPaddingLeft: number
    layoutPaddingRight: number
    layoutBorderTop: number
    layoutBorderBottom: number
    layoutBorderLeft: number
    layoutBorderRight: number
    hasNewLayout: boolean
    isDirty: boolean
    isReferenceBaseline: boolean
    scrollLeft: number
    scrollTop: number
    scrollableWidth: number
    scrollableHeight: number
    isClipping: boolean
    isDisplayed: boolean
    visualParent: Node|nil
    addChild: ((self: View, child: Node) -> nil)
    insertAt: ((self: View, index: integer, child: Node) -> nil)
    removeAt: ((self: View, index: integer) -> nil)
    scrollIntoView: ((self: View) -> nil)
    focus: ((self: View) -> nil)
    blur: ((self: View) -> nil)
end

declare View: {
}

declare extern type Direction with
end

declare Direction: {
    inherit: Direction,
    ltr: Direction,
    rtl: Direction,
}

declare extern type BaseMouseDragEvent with
    dragStart: LuaVector2
    position: LuaVector2
    button: integer
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseDragEvent, right: BaseMouseDragEvent) -> boolean)
end

declare BaseMouseDragEvent: {
}

declare extern type BaseMouseEvent with
    position: LuaVector2
    button: MouseButton
    buttons: MouseButtons
    ctrlKey: boolean
    altKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseEvent, right: BaseMouseEvent) -> boolean)
end

declare BaseMouseEvent: {
}

declare extern type BaseMouseMoveEvent with
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    altKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseMoveEvent, right: BaseMouseMoveEvent) -> boolean)
end

declare BaseMouseMoveEvent: {
}

declare extern type BaseMouseWheelEvent with
    delta: LuaVector3
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    __eq: ((left: BaseMouseWheelEvent, right: BaseMouseWheelEvent) -> boolean)
end

declare BaseMouseWheelEvent: {
}

declare extern type KeyboardEvent with
    keyChar: Key
    keyCode: Key
    keys: Keys
    __eq: ((left: KeyboardEvent, right: KeyboardEvent) -> boolean)
end

declare KeyboardEvent: {
}

declare extern type KeyboardTypingEvent with
    keyChar: integer
    __eq: ((left: KeyboardTypingEvent, right: KeyboardTypingEvent) -> boolean)
end

declare KeyboardTypingEvent: {
}

declare extern type MouseDragEvent with
    dragStart: LuaVector2
    relativeDragStart: LuaVector2
    position: LuaVector2
    button: integer
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseDragEvent, right: MouseDragEvent) -> boolean)
end

declare MouseDragEvent: {
}

declare extern type MouseEvent with
    position: LuaVector2
    button: MouseButton
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseEvent, right: MouseEvent) -> boolean)
end

declare MouseEvent: {
}

declare extern type MouseMoveEvent with
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseMoveEvent, right: MouseMoveEvent) -> boolean)
end

declare MouseMoveEvent: {
}

declare extern type MouseWheelEvent with
    delta: LuaVector3
    position: LuaVector2
    buttons: MouseButtons
    ctrlKey: boolean
    metaKey: boolean
    shiftKey: boolean
    relativePosition: LuaVector2
    __eq: ((left: MouseWheelEvent, right: MouseWheelEvent) -> boolean)
end

declare MouseWheelEvent: {
}

declare extern type TextNode extends Node with
    visualChildren: { Node }
    text: string
    visualParent: Node|nil
end

declare TextNode: {
}

