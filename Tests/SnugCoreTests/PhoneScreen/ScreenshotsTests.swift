import CoreGraphics
import Foundation
import Testing
@testable import SnugCore

@Suite("手机屏截图 Screenshots")
@MainActor
struct ScreenshotsTests {
    let shooter = FakeShooter()
    let timeline = ManualTimeline()
    let shots: Screenshots
    /// onChange 交出来的每一次变化
    let changes = Changes()

    init() {
        shots = Screenshots(system: shooter, timeline: timeline)
        let changes = changes
        shots.onChange = { changes.list.append($0) }
    }

    @Test("截图：整张进剪贴板，底部浮缩略图")
    func take() async {
        await shots.take(ghostty)
        #expect(shooter.captured == [ghostty.key])
        #expect(shooter.clipboard == FakeShooter.shot)
        #expect(shots.overlay == .preview(FakeShooter.shot))
        #expect(changes.list == [.preview(FakeShooter.shot)])
    }

    @Test("没有录屏权限：不截，也不弹系统授权，只提示去 Mac 上授权")
    func noPermission() async {
        shooter.trusted = false
        await shots.take(ghostty)
        #expect(shooter.captured.isEmpty)
        #expect(shots.overlay == .notice(.needsPermission))
    }

    @Test("截不成各报各的：窗口关了、系统没给画面、截到一半权限被收回；剪贴板不动", arguments: [
        (CaptureFailure.windowGone, ScreenshotNotice.windowGone),
        (.unavailable, .failed),
        (.noPermission, .needsPermission),
    ])
    func failures(failure: CaptureFailure, notice: ScreenshotNotice) async {
        shooter.results = [.failure(failure)]
        await shots.take(ghostty)
        #expect(shooter.clipboard == nil)
        #expect(shots.overlay == .notice(notice))
    }

    @Test("剪贴板没写进去：不浮缩略图，如实提示")
    func copyFailed() async {
        shooter.copyWorks = false
        await shots.take(ghostty)
        #expect(shots.overlay == .notice(.copyFailed))
    }

    @Test("缩略图 5 秒后收起，文字提示 3 秒后收起")
    func expires() async {
        await shots.take(ghostty)
        timeline.advance(Screenshots.previewDuration - 0.1)
        shots.expire()
        #expect(shots.overlay == .preview(FakeShooter.shot))
        timeline.advance(0.2)
        shots.expire()
        #expect(shots.overlay == nil)

        shooter.trusted = false
        await shots.take(ghostty)
        timeline.advance(Screenshots.noticeDuration + 0.1)
        shots.expire()
        #expect(shots.overlay == nil)
    }

    @Test("裁剪中不会到点收起；取消回到缩略图，重新计时")
    func cropDoesNotExpire() async {
        await shots.take(ghostty)
        shots.openCrop()
        #expect(shots.overlay == .cropping(FakeShooter.shot))
        timeline.advance(60)
        shots.expire()
        #expect(shots.overlay == .cropping(FakeShooter.shot))
        shots.cancelCrop()
        #expect(shots.overlay == .preview(FakeShooter.shot))
        timeline.advance(Screenshots.previewDuration - 0.1)
        shots.expire()
        #expect(shots.overlay == .preview(FakeShooter.shot))
        #expect(shooter.clipboard == FakeShooter.shot)
    }

    @Test("完成裁剪：选区按比例换算成原图像素，裁好的那块替换剪贴板")
    func finishCrop() async {
        await shots.take(ghostty)
        shots.openCrop()
        // 选区是图片里的比例：左上四分之一就是 1000×2000 原图的左上 500×1000
        shots.finishCrop(CGRect(x: 0, y: 0, width: 0.5, height: 0.5))
        #expect(shooter.crops == [CGRect(x: 0, y: 0, width: 500, height: 1000)])
        #expect(shooter.clipboard?.pixelWidth == 500)
        #expect(shooter.clipboard?.pixelHeight == 1000)
        #expect(shots.overlay == .notice(.cropped))
    }

    @Test("裁不出来：剪贴板里还是整张图，如实提示")
    func cropFailed() async {
        shooter.cropWorks = false
        await shots.take(ghostty)
        shots.openCrop()
        shots.finishCrop(CGRect(x: 0, y: 0, width: 0.5, height: 0.5))
        #expect(shooter.clipboard == FakeShooter.shot)
        #expect(shots.overlay == .notice(.cropFailed))
    }

    @Test("裁剪中手机屏没了：一并收掉，下次连上不会再冒出旧截图的裁剪界面")
    func dismissWhileCropping() async {
        await shots.take(ghostty)
        shots.openCrop()
        shots.dismiss()
        #expect(shots.overlay == nil)
        shots.finishCrop(CGRect(x: 0, y: 0, width: 0.5, height: 0.5))
        #expect(shooter.crops.isEmpty)
        #expect(shots.overlay == nil)
    }

    @Test("截到一半手机屏没了：截好的不再浮出来，也不写剪贴板")
    func dismissWhileCapturing() async {
        shooter.holding = true
        let taking = Task { await shots.take(ghostty) }
        await settle()
        shots.dismiss()
        shooter.release()
        await taking.value
        #expect(shots.overlay == nil)
        #expect(shooter.clipboard == nil)
    }

    @Test("连点：以最后一次为准，先点的晚截完也不顶掉后点的")
    func latestWins() async {
        let first = CapturedImage(png: Data([7]), pixelWidth: 10, pixelHeight: 20)
        shooter.results = [.success(first), .success(FakeShooter.shot)]
        shooter.holding = true
        let slow = Task { await shots.take(ghostty) }
        await settle()
        shooter.holding = false
        await shots.take(ghostty)
        shooter.release()
        await slow.value
        #expect(shots.overlay == .preview(FakeShooter.shot))
        #expect(shooter.clipboard == FakeShooter.shot)
    }

    @Test("裁剪中截完的一张不打断裁剪，也不改剪贴板")
    func captureDuringCrop() async {
        await shots.take(ghostty)
        let later = CapturedImage(png: Data([8]), pixelWidth: 10, pixelHeight: 20)
        shooter.results = [.success(later)]
        shooter.holding = true
        let taking = Task { await shots.take(ghostty) }
        await settle()
        shots.openCrop()
        shooter.release()
        await taking.value
        #expect(shots.overlay == .cropping(FakeShooter.shot))
        #expect(shooter.clipboard == FakeShooter.shot)
    }

    /// 让挂起的截图跑到 capture 里停住
    private func settle() async {
        for _ in 0..<10 { await Task.yield() }
    }
}

@MainActor
final class Changes {
    var list: [ScreenshotOverlay?] = []
}

/// 假的截图能力：截出来的图按 results 依次给（用完了给 shot），holding 时挂住直到 release
@MainActor
final class FakeShooter: ScreenshotControl {
    static let shot = CapturedImage(png: Data([1, 2, 3]), pixelWidth: 1000, pixelHeight: 2000)

    var trusted = true
    var results: [Result<CapturedImage, CaptureFailure>] = []
    var copyWorks = true
    var cropWorks = true
    var holding = false
    private(set) var captured: [WindowKey] = []
    private(set) var clipboard: CapturedImage?
    private(set) var crops: [CGRect] = []
    private var held: [CheckedContinuation<Void, Never>] = []

    func capture(_ window: WindowKey) async throws(CaptureFailure) -> CapturedImage {
        captured.append(window)
        let result = results.isEmpty ? .success(Self.shot) : results.removeFirst()
        if holding { await withCheckedContinuation { held.append($0) } }
        return try result.get()
    }

    func release() {
        let waiting = held
        held = []
        waiting.forEach { $0.resume() }
    }

    func crop(_ image: CapturedImage, to rect: CGRect) -> CapturedImage? {
        crops.append(rect)
        guard cropWorks else { return nil }
        return CapturedImage(png: Data([9]), pixelWidth: Int(rect.width), pixelHeight: Int(rect.height))
    }

    func copy(_ image: CapturedImage) -> Bool {
        guard copyWorks else { return false }
        clipboard = image
        return true
    }
}
