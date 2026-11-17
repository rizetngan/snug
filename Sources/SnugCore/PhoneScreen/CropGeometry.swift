// 截图裁剪的几何：图片等比铺在裁剪界面里；选区存成图片里的比例（0…1），界面换档、换尺寸都不用换算，
// 完成时再换算成原图像素。拖把手、抓把手也在这里。纯计算，坐标一律左上原点
import CoreGraphics

public enum CropGeometry {
    /// 八个把手：四角和四边
    public enum Handle: CaseIterable, Sendable {
        case topLeft, top, topRight, left, right, bottomLeft, bottom, bottomRight
    }

    /// 图片等比放进 area 后的显示范围（居中，可能上下或左右留白）
    public static func fittedRect(image: CGSize, in area: CGRect) -> CGRect {
        guard image.width > 0, image.height > 0, area.width > 0, area.height > 0 else { return .zero }
        let scale = min(area.width / image.width, area.height / image.height)
        let size = CGSize(width: image.width * scale, height: image.height * scale)
        return CGRect(x: area.midX - size.width / 2, y: area.midY - size.height / 2,
                      width: size.width, height: size.height)
    }

    /// 界面里的矩形换成图片里的比例。shown 是图片的显示范围
    public static func unitRect(_ rect: CGRect, in shown: CGRect) -> CGRect {
        guard shown.width > 0, shown.height > 0 else { return .zero }
        return CGRect(x: (rect.minX - shown.minX) / shown.width, y: (rect.minY - shown.minY) / shown.height,
                      width: rect.width / shown.width, height: rect.height / shown.height)
    }

    /// 图片里的比例换成界面里的矩形
    public static func rect(_ unit: CGRect, in shown: CGRect) -> CGRect {
        CGRect(x: shown.minX + unit.minX * shown.width, y: shown.minY + unit.minY * shown.height,
               width: unit.width * shown.width, height: unit.height * shown.height)
    }

    /// 比例换成原图像素：出了图片的部分不算，完全在图片外返回 .zero。四条边各自取整，右下边不会越出原图
    public static func pixelRect(_ unit: CGRect, image: CGSize) -> CGRect {
        let clipped = unit.intersection(CGRect(x: 0, y: 0, width: 1, height: 1))
        guard !clipped.isNull, clipped.width > 0, clipped.height > 0 else { return .zero }
        let minX = (clipped.minX * image.width).rounded()
        let minY = (clipped.minY * image.height).rounded()
        let maxX = (clipped.maxX * image.width).rounded()
        let maxY = (clipped.maxY * image.height).rounded()
        return CGRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY)
    }

    /// 按下的地方抓住哪个把手：离两条边都够近算角，离一条边够近（且在这条边的范围内）算边，
    /// 选区很小、两条对边都够得着时取近的那条；都不近返回 nil
    public static func handle(at point: CGPoint, of rect: CGRect, radius: CGFloat) -> Handle? {
        guard rect.insetBy(dx: -radius, dy: -radius).contains(point) else { return nil }
        switch (nearer(point.x, rect.minX, rect.maxX, radius), nearer(point.y, rect.minY, rect.maxY, radius)) {
        case (.min?, .min?): return .topLeft
        case (.max?, .min?): return .topRight
        case (.min?, .max?): return .bottomLeft
        case (.max?, .max?): return .bottomRight
        case (.min?, nil): return .left
        case (.max?, nil): return .right
        case (nil, .min?): return .top
        case (nil, .max?): return .bottom
        case (nil, nil): return nil
        }
    }

    /// 拖动把手：对应的边或角按手指移动的距离挪（按下不会跳到手指底下），限制在 bounds 里，宽高不小于 minSide
    public static func resized(_ rect: CGRect, _ handle: Handle, by offset: CGSize, within bounds: CGRect, minSide: CGFloat) -> CGRect {
        var minX = rect.minX, maxX = rect.maxX, minY = rect.minY, maxY = rect.maxY
        switch handle {
        case .topLeft, .left, .bottomLeft: minX = min(max(rect.minX + offset.width, bounds.minX), maxX - minSide)
        case .topRight, .right, .bottomRight: maxX = max(min(rect.maxX + offset.width, bounds.maxX), minX + minSide)
        case .top, .bottom: break
        }
        switch handle {
        case .topLeft, .top, .topRight: minY = min(max(rect.minY + offset.height, bounds.minY), maxY - minSide)
        case .bottomLeft, .bottom, .bottomRight: maxY = max(min(rect.maxY + offset.height, bounds.maxY), minY + minSide)
        case .left, .right: break
        }
        return CGRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY)
    }

    private enum Side { case min, max }

    /// value 离 lower、upper 哪一边够近（radius 以内），都近取更近的
    private static func nearer(_ value: CGFloat, _ lower: CGFloat, _ upper: CGFloat, _ radius: CGFloat) -> Side? {
        let toMin = abs(value - lower), toMax = abs(value - upper)
        guard min(toMin, toMax) <= radius else { return nil }
        return toMin <= toMax ? .min : .max
    }
}
