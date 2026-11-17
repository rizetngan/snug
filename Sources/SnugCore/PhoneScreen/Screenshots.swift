// 手机屏上的截图：点小横条左边的相机键，截下铺满手机屏的窗口，整张放进电脑剪贴板，底部浮一张缩略图；
// 点缩略图进裁剪，裁好的那块替换剪贴板里的整张图。界面只按 overlay 画，到点收起由 AppController 每拍调 expire。
// 截、裁、写剪贴板都要系统接口，定成 ScreenshotControl：实现在 SnugSystem（ScreenshotService），测试里换成假的
import CoreGraphics
import Foundation

/// 截好的一张图（PNG）
public struct CapturedImage: Equatable, Sendable {
    public let png: Data
    public let pixelWidth: Int
    public let pixelHeight: Int

    public init(png: Data, pixelWidth: Int, pixelHeight: Int) {
        self.png = png
        self.pixelWidth = pixelWidth
        self.pixelHeight = pixelHeight
    }

    public var size: CGSize { CGSize(width: pixelWidth, height: pixelHeight) }
}

/// 截图没成的几种情况
public enum CaptureFailure: Error, Equatable, Sendable {
    /// 没有录屏权限（截到一半被收回也算）
    case noPermission
    /// 窗口已经关了
    case windowGone
    /// 系统这次没给画面（锁屏、窗口刚收起、截流失败）
    case unavailable
}

/// 截图要用到的系统能力（ScreenCaptureKit、NSPasteboard）
@MainActor
public protocol ScreenshotControl: AnyObject {
    /// 有没有「录屏与系统录音」权限。只读状态，不弹系统授权
    var trusted: Bool { get }
    /// 截窗口的完整画面（被别的窗口挡着也截得到）
    func capture(_ window: WindowKey) async throws(CaptureFailure) -> CapturedImage
    /// 按像素矩形（左上原点）裁出一块；裁不出来返回 nil
    func crop(_ image: CapturedImage, to rect: CGRect) -> CapturedImage?
    /// 清空剪贴板，只放这一张；没写进去返回 false
    func copy(_ image: CapturedImage) -> Bool
}

/// 手机屏上此刻浮着的截图界面
public enum ScreenshotOverlay: Equatable, Sendable {
    /// 刚截好：底部浮缩略图，点它进裁剪
    case preview(CapturedImage)
    /// 正在裁剪：盖满手机屏，手势翻译要停掉（拖动是在画选区）
    case cropping(CapturedImage)
    /// 一句话的结果
    case notice(ScreenshotNotice)

    public var isCropping: Bool {
        if case .cropping = self { true } else { false }
    }
}

/// 截图、裁剪的结果提示。措辞归界面层
public enum ScreenshotNotice: Equatable, Sendable {
    case cropped
    case needsPermission
    case windowGone
    case failed
    case copyFailed
    case cropFailed
}

@MainActor
public final class Screenshots {
    /// 缩略图留多久：比文字提示长，留出点它的时间
    public static let previewDuration: TimeInterval = 5
    public static let noticeDuration: TimeInterval = 3

    public private(set) var overlay: ScreenshotOverlay?
    /// overlay 变了（AppController 接到 AppState 上，并按是否在裁剪开关手势翻译）
    public var onChange: ((ScreenshotOverlay?) -> Void)?

    private let system: any ScreenshotControl
    private let timeline: any Timeline
    private var expiresAt: TimeInterval?
    /// 每点一次相机键加一：截完时号码对不上（又点了一次、手机屏没了）就不再显示
    private var serial = 0

    public init(system: any ScreenshotControl, timeline: any Timeline = SystemTimeline()) {
        self.system = system
        self.timeline = timeline
    }

    public var trusted: Bool { system.trusted }

    /// 截 window，整张放进剪贴板。连点时以最后一次为准；裁剪中不截
    public func take(_ window: WindowInfo) async {
        guard overlay?.isCropping != true else { return }
        guard system.trusted else {
            Log.info("截图：没有录屏权限，提示去 Mac 上授权")
            return set(.notice(.needsPermission))
        }
        serial += 1
        let mine = serial
        let started = timeline.now
        let result: Result<CapturedImage, CaptureFailure>
        do throws(CaptureFailure) {
            result = .success(try await system.capture(window.key))
        } catch {
            result = .failure(error)
        }
        // 截的这段时间里又点了一次、手机屏没了、或者进了裁剪：这一张作废，剪贴板也不动
        guard mine == serial, overlay?.isCropping != true else {
            Log.info("截图：\(window.label) 截完时已不需要，丢弃")
            return
        }
        switch result {
        case .success(let image):
            guard system.copy(image) else {
                Log.warn("截图：剪贴板没写进去（\(window.label)）")
                return set(.notice(.copyFailed))
            }
            Log.info("截图：\(window.label) → 剪贴板 \(image.pixelWidth)×\(image.pixelHeight)，\(image.png.count) 字节，"
                     + String(format: "%.2fs", timeline.now - started))
            set(.preview(image))
        case .failure(let failure):
            Log.warn("截图失败（\(window.label)）：\(failure)")
            set(.notice(failure.notice))
        }
    }

    /// 点缩略图：进裁剪，不再到点收起
    public func openCrop() {
        guard case .preview(let image) = overlay else { return }
        set(.cropping(image))
    }

    /// 取消裁剪：回到缩略图，重新计时，剪贴板里还是整张图
    public func cancelCrop() {
        guard case .cropping(let image) = overlay else { return }
        set(.preview(image))
    }

    /// 完成裁剪：selection 是图片里的比例（0…1，左上原点，见 CropGeometry）
    public func finishCrop(_ selection: CGRect) {
        guard case .cropping(let image) = overlay else { return }
        let rect = CropGeometry.pixelRect(selection, image: image.size)
        guard let cropped = system.crop(image, to: rect) else {
            Log.warn("截图裁剪：\(image.pixelWidth)×\(image.pixelHeight) 裁不出 \(rect)，剪贴板里留着整张图")
            return set(.notice(.cropFailed))
        }
        guard system.copy(cropped) else {
            Log.warn("截图裁剪：剪贴板没写进去")
            return set(.notice(.copyFailed))
        }
        Log.info("截图裁剪：\(image.pixelWidth)×\(image.pixelHeight) 的 \(rect) → 剪贴板，\(cropped.png.count) 字节")
        set(.notice(.cropped))
    }

    /// 到点收起（AppController 每拍调）。裁剪中不收
    public func expire() {
        guard let expiresAt, timeline.now >= expiresAt else { return }
        set(nil)
    }

    /// 手机屏没了：浮着的、裁剪中的、正在截的一并作废，下次连上从干净的状态开始
    public func dismiss() {
        serial += 1
        set(nil)
    }

    /// 换浮层，按它是哪一种重新计时
    private func set(_ overlay: ScreenshotOverlay?) {
        expiresAt = Self.lifetime(overlay).map { timeline.now + $0 }
        guard overlay != self.overlay else { return }
        self.overlay = overlay
        onChange?(overlay)
    }
}

extension Screenshots {
    /// 浮多久自己收起：缩略图、提示到点收，裁剪中不收
    private static func lifetime(_ overlay: ScreenshotOverlay?) -> TimeInterval? {
        switch overlay {
        case .preview?: previewDuration
        case .notice?: noticeDuration
        case .cropping?, nil: nil
        }
    }
}

private extension CaptureFailure {
    var notice: ScreenshotNotice {
        switch self {
        case .noPermission: .needsPermission
        case .windowGone: .windowGone
        case .unavailable: .failed
        }
    }
}
