import Cocoa
import CoreGraphics

// Local state, independent of UI so schedules and interaction can be verified.
enum Mood: Int { case normal = 0, bored, hungry, shy, angry, eating }
struct PetState {
    var hungry = false
    var lastMealCheck: Date
    var touches = 0
    var lastTouch: Date?
    var eatingUntil = Date.distantPast
    static var calendar: Calendar { var c = Calendar(identifier: .gregorian); c.timeZone = TimeZone(identifier: "Asia/Shanghai")!; return c }
    mutating func update(now: Date) -> Bool {
        let c = Self.calendar
        let start = c.startOfDay(for: now)
        let yesterday = c.date(byAdding: .day, value: -1, to: start)!
        let latest = [yesterday, start].flatMap { day in [8, 12, 18].compactMap { c.date(bySettingHour: $0, minute: 0, second: 0, of: day) } }.last { $0 <= now }
        var meal = false
        if let latest = latest, latest > lastMealCheck { hungry = true; lastMealCheck = latest; meal = true }
        if let touch = lastTouch, now.timeIntervalSince(touch) >= 15 { touches = 0; lastTouch = nil }
        return meal
    }
    mutating func touch(now: Date) { if let t = lastTouch, now.timeIntervalSince(t) >= 15 { touches = 0 }; touches += 1; lastTouch = now }
    mutating func feed(now: Date) { hungry = false; eatingUntil = now.addingTimeInterval(5); touches = 0; lastTouch = nil }
    func mood(now: Date, idle: Double) -> Mood {
        if hungry { return .hungry }
        if now < eatingUntil { return .eating }
        if touches >= 20 { return .angry }
        if touches >= 5 { return .shy }
        if idle >= 15 { return .bored }
        return .normal
    }
}

final class PetView: NSView {
    var sprites: [NSImage] = []
    var mood: Mood = .normal
    var previousMood: Mood = .normal
    var changedAt = Date.distantPast
    var message = "你好呀～右键菜单里可以喂米饭！"
    var messageUntil = Date().addingTimeInterval(6)
    var dragging = false
    var moved = false
    var downMouse = NSPoint.zero
    var lastMouse = NSPoint.zero
    weak var owner: PetController?
    override var acceptsFirstResponder: Bool { true }
    func setMood(_ value: Mood, animated: Bool = true) {
        if mood != value { previousMood = mood; mood = value; changedAt = animated ? Date() : .distantPast; needsDisplay = true }
        if !animated { changedAt = .distantPast; needsDisplay = true }
    }
    override func draw(_ dirtyRect: NSRect) {
        let area = NSRect(x: 8, y: 4, width: bounds.width - 16, height: bounds.height - 65)
        let side = min(area.width, area.height)
        let rect = NSRect(x: bounds.midX - side / 2, y: area.minY, width: side, height: side)
        let progress = min(1, Date().timeIntervalSince(changedAt) / 0.22)
        if sprites.count == 6 {
            if progress < 1 { sprites[previousMood.rawValue].draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1 - progress) }
            sprites[mood.rawValue].draw(in: rect, from: .zero, operation: .sourceOver, fraction: progress)
        }
        // Animate the mood indicator, keeping the character and window still.
        let pulse = CGFloat(0.6 + 0.4 * sin(Date().timeIntervalSinceReferenceDate * 2))
        let symbol: String
        switch mood { case .bored: symbol = "⋯"; case .hungry: symbol = "🍚"; case .shy: symbol = "♡"; case .angry: symbol = "💢"; case .eating: symbol = "♪"; default: symbol = "" }
        if !symbol.isEmpty { (symbol as NSString).draw(at: NSPoint(x: bounds.width - 34, y: bounds.height - 82), withAttributes: [.font: NSFont.systemFont(ofSize: 22), .foregroundColor: NSColor.systemPink.withAlphaComponent(pulse)]) }
        let persistent = mood == .hungry || mood == .bored
        if Date() < messageUntil || persistent {
            let text = Date() < messageUntil ? message : (mood == .hungry ? "肚子饿啦…右键菜单喂饭 🍚" : "有点无聊…陪我玩一下嘛～")
            let bubble = NSRect(x: 5, y: bounds.height - 56, width: bounds.width - 10, height: 46)
            NSColor(calibratedWhite: 1, alpha: 0.97).setFill(); NSBezierPath(roundedRect: bubble, xRadius: 14, yRadius: 14).fill()
            let p = NSMutableParagraphStyle(); p.alignment = .center
            (text as NSString).draw(in: bubble.insetBy(dx: 8, dy: 8), withAttributes: [.font: NSFont.systemFont(ofSize: 12, weight: .medium), .foregroundColor: NSColor(calibratedRed: 0.16, green: 0.25, blue: 0.44, alpha: 1), .paragraphStyle: p])
        }
    }
    override func mouseDown(with event: NSEvent) { downMouse = NSEvent.mouseLocation; lastMouse = downMouse; dragging = true; moved = false }
    override func mouseDragged(with event: NSEvent) {
        guard let w = window else { return }
        let point = NSEvent.mouseLocation
        if hypot(point.x - downMouse.x, point.y - downMouse.y) >= 3 { moved = true }
        if moved { w.setFrameOrigin(NSPoint(x: w.frame.minX + point.x - lastMouse.x, y: w.frame.minY + point.y - lastMouse.y)) }
        lastMouse = point
    }
    override func mouseUp(with event: NSEvent) {
        dragging = false
        if moved { owner?.savePosition(); return }
        owner?.touch()
    }
    override func rightMouseDown(with event: NSEvent) { if let menu = owner?.makeMenu() { NSMenu.popUpContextMenu(menu, with: event, for: self) } }
}

struct SavedSettings: Codable {
    var hungry = false
    var lastMealCheck = PetState.calendar.startOfDay(for: Date())
    var width: Double = 250
    var x: Double? = nil
    var y: Double? = nil
}
final class PetController: NSObject, NSApplicationDelegate {
    var panel: NSPanel!
    var pet: PetView!
    var status: NSStatusItem!
    var timer: Timer?
    var model: PetState
    var preview: Mood?
    var previewUntil = Date.distantPast
    var settings: SavedSettings
    let root = Bundle.main.bundleURL.deletingLastPathComponent()
    var settingsURL: URL { root.appendingPathComponent("Data/settings.json") }
    override init() {
        let url = Bundle.main.bundleURL.deletingLastPathComponent().appendingPathComponent("Data/settings.json")
        settings = (try? JSONDecoder().decode(SavedSettings.self, from: Data(contentsOf: url))) ?? SavedSettings()
        model = PetState(hungry: settings.hungry, lastMealCheck: settings.lastMealCheck)
        super.init()
    }
    func applicationDidFinishLaunching(_ notification: Notification) {
        if NSRunningApplication.runningApplications(withBundleIdentifier: "local.whale.pet").count > 1 { NSApp.terminate(nil); return }
        NSApp.setActivationPolicy(.accessory)
        let width = settings.width
        let size: CGFloat = width >= 150 && width <= 380 ? width : 250
        panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: size, height: size + 65), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = false; panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        pet = PetView(frame: NSRect(origin: .zero, size: panel.frame.size)); pet.owner = self
        if let url = Bundle.main.url(forResource: "states", withExtension: "png"), let image = NSImage(contentsOf: url), let cg = image.cgImage(forProposedRect: nil, context: nil, hints: nil) {
            for row in 0..<2 { for col in 0..<3 {
                let split = cg.height / 2 + 3 // The generated sheet places the separating gap at y=515.
                let cell = CGRect(x: col * cg.width / 3, y: row == 0 ? 0 : split, width: cg.width / 3, height: row == 0 ? split : cg.height - split)
                if let crop = cg.cropping(to: cell) { pet.sprites.append(NSImage(cgImage: crop, size: NSSize(width: crop.width, height: crop.height))) }
            } }
        }
        panel.contentView = pet
        if let screen = NSScreen.main { panel.setFrameOrigin(NSPoint(x: screen.visibleFrame.maxX - size - 30, y: screen.visibleFrame.minY + 10)) }
        if let x = settings.x, let y = settings.y {
            let origin = NSPoint(x: x, y: y)
            let frame = NSRect(origin: origin, size: panel.frame.size)
            if NSScreen.screens.contains(where: { $0.visibleFrame.intersection(frame).width > 80 && $0.visibleFrame.intersection(frame).height > 80 }) { panel.setFrameOrigin(origin) }
        }
        panel.orderFrontRegardless()
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        status.button?.title = "🐳"; status.button?.toolTip = "鲸鱼娘桌宠 · 右键打开菜单"; status.menu = makeMenu()
        tick()
        timer = Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(timer!, forMode: .common)
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(wake), name: NSWorkspace.didWakeNotification, object: nil)
    }
    func persist() {
        do { try FileManager.default.createDirectory(at: settingsURL.deletingLastPathComponent(), withIntermediateDirectories: true); try JSONEncoder().encode(settings).write(to: settingsURL, options: .atomic) }
        catch { NSLog("Could not save pet settings: %@", error.localizedDescription) }
    }
    func saveState() { settings.hungry = model.hungry; settings.lastMealCheck = model.lastMealCheck; persist() }
    func savePosition() { settings.x = panel.frame.minX; settings.y = panel.frame.minY; settings.width = panel.frame.width; persist() }
    func speak(_ text: String) { pet.message = text; pet.messageUntil = Date().addingTimeInterval(4); pet.needsDisplay = true }
    func tick() {
        let now = Date()
        if model.update(now: now) { saveState(); speak("到饭点啦，肚子咕咕叫…🍚") }
        let idle = CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: CGEventType(rawValue: UInt32.max)!)
        if let selected = preview, now < previewUntil { pet.setMood(selected, animated: false) }
        else { preview = nil; pet.setMood(model.mood(now: now, idle: idle)) }
        pet.needsDisplay = true
    }
    @objc func wake() { tick() }
    func touch() {
        preview = nil
        model.touch(now: Date())
        if model.touches >= 20 { speak("哼！头发都要被你摸乱啦！") }
        else if model.touches >= 5 { speak("嘿嘿…有点不好意思了～") }
        else { speak("摸摸头，今天也要加油呀～") }
        tick()
    }
    @objc func feed() { preview = nil; _ = model.update(now: Date()); model.feed(now: Date()); saveState(); speak("啊呜～米饭最好吃了！🍚"); tick() }
    func makeMenu() -> NSMenu {
        let menu = NSMenu()
        func add(_ title: String, _ action: Selector) { let i = NSMenuItem(title: title, action: action, keyEquivalent: ""); i.target = self; menu.addItem(i) }
        add("🍚 喂米饭", #selector(feed))
        add("🐳 打开 DeepSeek", #selector(openDeepSeek))
        add("小一点", #selector(smaller)); add("大一点", #selector(larger)); add("回到右下角", #selector(reset))
        let preview = NSMenu(); let item = NSMenuItem(title: "预览表情", action: nil, keyEquivalent: "")
        for (n, title) in ["正常", "无聊", "饥饿", "害羞", "嗔怒", "吃饭"].enumerated() { let i = NSMenuItem(title: title, action: #selector(previewMood(_:)), keyEquivalent: ""); i.tag = n; i.target = self; preview.addItem(i) }
        item.submenu = preview; menu.addItem(item)
        menu.addItem(.separator()); add("关闭登录自启动", #selector(disableStartup)); add("退出鲸鱼娘", #selector(quit)); return menu
    }
    @objc func openDeepSeek() {
        let home = FileManager.default.homeDirectoryForCurrentUser
        let candidates = [home.appendingPathComponent("Desktop/DeepSeek.app"), home.appendingPathComponent("Applications/Chrome Apps.localized/DeepSeek.app"), home.appendingPathComponent("Applications/DeepSeek.app"), URL(fileURLWithPath: "/Applications/DeepSeek.app")]
        guard let url = candidates.first(where: { FileManager.default.fileExists(atPath: $0.path) }) else {
            speak("找不到 DeepSeek 应用，请确认安装位置")
            return
        }
        let config = NSWorkspace.OpenConfiguration(); config.activates = true
        NSWorkspace.shared.openApplication(at: url, configuration: config) { [weak self] _, error in
            if error != nil { DispatchQueue.main.async { self?.speak("DeepSeek 应用打开失败，请稍后再试") } }
        }
    }
    @objc func previewMood(_ item: NSMenuItem) {
        guard let m = Mood(rawValue: item.tag) else { return }
        preview = m; previewUntil = Date().addingTimeInterval(4)
        pet.setMood(m, animated: false)
        speak("表情预览：" + item.title)
        pet.displayIfNeeded()
    }
    func resize(_ factor: CGFloat) { let w = min(380, max(150, panel.frame.width * factor)); panel.setFrame(NSRect(origin: panel.frame.origin, size: NSSize(width: w, height: w + 65)), display: true); pet.frame = NSRect(origin: .zero, size: panel.frame.size); savePosition() }
    @objc func smaller() { resize(0.85) }
    @objc func larger() { resize(1.15) }
    @objc func reset() { if let s = NSScreen.main { panel.setFrameOrigin(NSPoint(x: s.visibleFrame.maxX - panel.frame.width - 30, y: s.visibleFrame.minY + 10)) }; if pet != nil { savePosition() } }
    @objc func disableStartup() {
        let url = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/LaunchAgents/local.whale.pet.plist")
        do { if FileManager.default.fileExists(atPath: url.path) { try FileManager.default.removeItem(at: url) }; speak("已关闭登录自启动，下次登录生效") } catch { speak("未能关闭自启动，请查看说明") }
    }
    @objc func quit() { saveState(); savePosition(); NSApp.terminate(nil) }
}

func selfTest() {
    let f = ISO8601DateFormatter()
    func date(_ s: String) -> Date { f.date(from: "2026-10-01T" + s + "+08:00")! }
    var m = PetState(lastMealCheck: date("07:59:59"))
    assert(!m.update(now: date("07:59:59")))
    assert(m.update(now: date("08:00:00")) && m.hungry)
    assert(!m.update(now: date("08:00:01")))
    m.feed(now: date("08:00:02")); assert(!m.hungry)
    assert(!m.update(now: date("11:59:59")))
    assert(m.update(now: date("12:00:00")) && m.hungry)
    m.feed(now: date("12:00:01")); assert(m.update(now: date("18:00:00")))
    m.feed(now: date("18:00:01"))
    assert(m.mood(now: date("18:00:02"), idle: 20) == .eating)
    for _ in 0..<5 { m.touch(now: date("18:01:00")) }
    assert(m.mood(now: date("18:01:00"), idle: 0) == .shy)
    for _ in 0..<15 { m.touch(now: date("18:01:01")) }
    assert(m.mood(now: date("18:01:01"), idle: 0) == .angry)
    _ = m.update(now: date("18:01:15")); assert(m.touches == 20)
    _ = m.update(now: date("18:01:16")); assert(m.touches == 0)
    assert(m.mood(now: date("18:01:16"), idle: 15) == .bored)
    assert(m.mood(now: date("18:01:16"), idle: 0) == .normal)
    m.hungry = true; m.touches = 20; assert(m.mood(now: date("18:01:17"), idle: 30) == .hungry)
    let next = f.date(from: "2026-10-02T12:30:00+08:00")!
    m.feed(now: date("18:02:00")); assert(m.update(now: next) && m.hungry)
    var overnight = PetState(lastMealCheck: date("12:00:00"))
    let midnight = f.date(from: "2026-10-02T01:00:00+08:00")!
    assert(overnight.update(now: midnight) && overnight.hungry)
    print("PASS: overnight missed meal, meal boundaries, no repeated hunger after feeding, wake catch-up, mood priority, 5/20 touches, 15-second recovery, idle threshold")
}
func testPreview() {
    _ = NSApplication.shared
    let c = PetController()
    c.model = PetState(hungry: true, lastMealCheck: Date())
    c.pet = PetView(frame: NSRect(x: 0, y: 0, width: 250, height: 315))
    c.timer = Timer(timeInterval: 0.1, repeats: true) { _ in }
    let refresh = c.timer!
    for n in 0..<6 {
        let item = NSMenuItem(title: "Test", action: nil, keyEquivalent: ""); item.tag = n
        c.previewMood(item)
        assert(c.pet.mood.rawValue == n && c.pet.changedAt == .distantPast)
        c.tick()
        assert(c.pet.mood.rawValue == n)
        assert(c.timer === refresh && refresh.isValid)
    }
    c.previewUntil = .distantPast; c.tick()
    assert(c.preview == nil && c.pet.mood == .hungry)
    let item = NSMenuItem(title: "Test", action: nil, keyEquivalent: ""); item.tag = 4
    c.previewMood(item); c.touch()
    assert(c.preview == nil && c.pet.mood == .hungry)
    refresh.invalidate()
    print("PASS: all six previews display immediately, refresh timer preserved, rapid switching, expiry restores hunger, petting cancels preview")
}
func renderPreview() {
    let args = CommandLine.arguments
    guard args.count >= 4 else { fatalError("--render-preview <sheet> <output directory>") }
    _ = NSApplication.shared
    let source = NSImage(contentsOfFile: args[2])!
    let cg = source.cgImage(forProposedRect: nil, context: nil, hints: nil)!
    var sprites: [NSImage] = []
    for row in 0..<2 { for col in 0..<3 { let split = cg.height / 2 + 3; let r = CGRect(x: col * cg.width / 3, y: row == 0 ? 0 : split, width: cg.width / 3, height: row == 0 ? split : cg.height - split); let crop = cg.cropping(to: r)!; sprites.append(NSImage(cgImage: crop, size: NSSize(width: crop.width, height: crop.height))) } }
    try! FileManager.default.createDirectory(atPath: args[3], withIntermediateDirectories: true)
    for n in 0..<6 {
        let view = PetView(frame: NSRect(x: 0, y: 0, width: 250, height: 315)); view.sprites = sprites; view.mood = Mood(rawValue: n)!; view.messageUntil = .distantPast
        let image = NSImage(size: view.bounds.size)
        image.lockFocus(); NSColor.white.setFill(); view.bounds.fill(); view.draw(view.bounds); image.unlockFocus()
        let rep = NSBitmapImageRep(data: image.tiffRepresentation!)!
        try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: args[3]).appendingPathComponent("preview-\(n).png"))
    }
    print("PASS: all six sprites loaded and rendered through PetView")
}
if CommandLine.arguments.contains("--self-test") { selfTest() }
else if CommandLine.arguments.contains("--test-preview") { testPreview() }
else if CommandLine.arguments.contains("--render-preview") { renderPreview() }
else { let app = NSApplication.shared; let controller = PetController(); app.delegate = controller; app.run() }
