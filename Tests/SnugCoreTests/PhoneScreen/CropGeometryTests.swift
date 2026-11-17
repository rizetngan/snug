import CoreGraphics
import Testing
@testable import SnugCore

@Suite("截图裁剪的几何 CropGeometry")
struct CropGeometryTests {
    /// 图片 1000×2000
    private let tall = CGSize(width: 1000, height: 2000)

    @Test("等比铺进去正中：宽高比一致时正好铺满，比例不同时留白也居中")
    func fitted() {
        let area = CGRect(x: 0, y: 0, width: 500, height: 1000)
        #expect(CropGeometry.fittedRect(image: tall, in: area) == area)
        // 400×400 的方图放进 500×1000：上下留白
        let square = CGSize(width: 400, height: 400)
        #expect(CropGeometry.fittedRect(image: square, in: area) == CGRect(x: 0, y: 250, width: 500, height: 500))
    }

    @Test("选区存成图片里的比例：和界面矩形来回换算不变，图片不在原点时先减去偏移")
    func unit() {
        let shown = CGRect(x: 30, y: 60, width: 500, height: 1000)
        let selection = CGRect(x: 30 + 250, y: 60 + 500, width: 125, height: 250)
        let unit = CropGeometry.unitRect(selection, in: shown)
        #expect(unit == CGRect(x: 0.5, y: 0.5, width: 0.25, height: 0.25))
        #expect(CropGeometry.rect(unit, in: shown) == selection)
    }

    @Test("比例换算成像素：整张就是整张图，右下四分之一就是原图右下四分之一")
    func pixels() {
        #expect(CropGeometry.pixelRect(CGRect(x: 0, y: 0, width: 1, height: 1), image: tall) == CGRect(x: 0, y: 0, width: 1000, height: 2000))
        #expect(CropGeometry.pixelRect(CGRect(x: 0.5, y: 0.5, width: 0.5, height: 0.5), image: tall)
                == CGRect(x: 500, y: 1000, width: 500, height: 1000))
    }

    @Test("出了图片的部分不算；完全在图片外就什么也没有")
    func clipped() {
        #expect(CropGeometry.pixelRect(CGRect(x: -0.2, y: -0.1, width: 0.4, height: 0.3), image: tall) == CGRect(x: 0, y: 0, width: 200, height: 400))
        #expect(CropGeometry.pixelRect(CGRect(x: 1.2, y: 0, width: 0.1, height: 0.1), image: tall) == .zero)
    }

    @Test("按边取整：两边各自四舍五入，右下边不会比原图多出一像素")
    func rounding() {
        // 左上边换算成 0.5 像素，右下边正好落在原图边上：按「起点 + 宽」取整会多出一像素
        let rect = CropGeometry.pixelRect(CGRect(x: 0.0005, y: 0.00025, width: 0.9995, height: 0.99975), image: tall)
        #expect(rect == CGRect(x: 1, y: 1, width: 999, height: 1999))
    }

    @Test("抓把手：离角近算角，整条边上哪儿都能抓（不只是中点），选区中间和离得远的不算")
    func grab() {
        let sel = CGRect(x: 100, y: 100, width: 200, height: 400)
        #expect(CropGeometry.handle(at: CGPoint(x: 108, y: 92), of: sel, radius: 30) == .topLeft)
        #expect(CropGeometry.handle(at: CGPoint(x: 290, y: 510), of: sel, radius: 30) == .bottomRight)
        #expect(CropGeometry.handle(at: CGPoint(x: 95, y: 180), of: sel, radius: 30) == .left)
        #expect(CropGeometry.handle(at: CGPoint(x: 310, y: 420), of: sel, radius: 30) == .right)
        #expect(CropGeometry.handle(at: CGPoint(x: 160, y: 104), of: sel, radius: 30) == .top)
        #expect(CropGeometry.handle(at: CGPoint(x: 200, y: 300), of: sel, radius: 30) == nil)
        #expect(CropGeometry.handle(at: CGPoint(x: 50, y: 300), of: sel, radius: 30) == nil)
        // 在左边的延长线上、但超出这条边的范围
        #expect(CropGeometry.handle(at: CGPoint(x: 100, y: 600), of: sel, radius: 30) == nil)
    }

    @Test("选区很小时两条边都够得着：抓离得近的那条")
    func grabSmall() {
        let sel = CGRect(x: 100, y: 100, width: 40, height: 40)
        #expect(CropGeometry.handle(at: CGPoint(x: 112, y: 135), of: sel, radius: 30) == .bottomLeft)
    }

    @Test("拖把手按手指移动的距离挪：按下不跳；只动对应的边或角")
    func drag() {
        let sel = CGRect(x: 100, y: 100, width: 200, height: 400)
        let bounds = CGRect(x: 0, y: 0, width: 500, height: 1000)
        #expect(CropGeometry.resized(sel, .topLeft, by: .zero, within: bounds, minSide: 44) == sel)
        #expect(CropGeometry.resized(sel, .left, by: CGSize(width: 50, height: 80), within: bounds, minSide: 44)
                == CGRect(x: 150, y: 100, width: 150, height: 400))
        #expect(CropGeometry.resized(sel, .bottomRight, by: CGSize(width: -20, height: -30), within: bounds, minSide: 44)
                == CGRect(x: 100, y: 100, width: 180, height: 370))
    }

    @Test("拖出图片会停在图片边上；收得再小也留 minSide")
    func dragLimits() {
        let sel = CGRect(x: 100, y: 100, width: 200, height: 400)
        let bounds = CGRect(x: 0, y: 0, width: 500, height: 1000)
        #expect(CropGeometry.resized(sel, .topLeft, by: CGSize(width: -500, height: -500), within: bounds, minSide: 44)
                == CGRect(x: 0, y: 0, width: 300, height: 500))
        #expect(CropGeometry.resized(sel, .right, by: CGSize(width: -1000, height: 0), within: bounds, minSide: 44)
                == CGRect(x: 100, y: 100, width: 44, height: 400))
    }
}
