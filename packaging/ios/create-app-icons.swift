import AppKit
import Foundation

// Use the same source artwork and background as packaging/macos/create-macos-icon.swift.
guard CommandLine.arguments.count == 3 else {
    fatalError("Usage: create-app-icons.swift SOURCE_IMAGE APPICON.appiconset")
}

let sourcePath = CommandLine.arguments[1]
let outputDirectory = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
guard let image = NSImage(contentsOfFile: sourcePath),
      let source = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
    fatalError("Cannot read image: \(sourcePath)")
}
try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)

let slots: [(size: Double, scale: Int, idiom: String)] = [
    (20, 2, "iphone"), (20, 3, "iphone"),
    (29, 2, "iphone"), (29, 3, "iphone"),
    (40, 2, "iphone"), (40, 3, "iphone"),
    (60, 2, "iphone"), (60, 3, "iphone"),
    (20, 1, "ipad"), (20, 2, "ipad"),
    (29, 1, "ipad"), (29, 2, "ipad"),
    (40, 1, "ipad"), (40, 2, "ipad"),
    (76, 1, "ipad"), (76, 2, "ipad"),
    (83.5, 2, "ipad"), (1024, 1, "ios-marketing")
]
var images: [[String: String]] = []
for slot in slots {
    let pixels = Int(slot.size * Double(slot.scale))
    let filename = "icon-\(pixels).png"
    // iOS masks the square icon itself. Export RGB without an alpha channel.
    guard let context = CGContext(data: nil, width: pixels, height: pixels,
        bitsPerComponent: 8, bytesPerRow: 0, space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue) else {
        fatalError("Cannot create icon context")
    }
    context.setFillColor(CGColor(red: 23.0 / 255.0, green: 25.0 / 255.0, blue: 28.0 / 255.0, alpha: 1))
    context.fill(CGRect(x: 0, y: 0, width: pixels, height: pixels))
    context.interpolationQuality = .high
    context.draw(source, in: CGRect(x: 0, y: 0, width: pixels, height: pixels))
    guard let output = context.makeImage(),
          let data = NSBitmapImageRep(cgImage: output).representation(using: .png, properties: [:]) else {
        fatalError("Cannot encode icon")
    }
    try data.write(to: outputDirectory.appendingPathComponent(filename))
    let size = slot.size == slot.size.rounded() ? String(Int(slot.size)) : String(slot.size)
    images.append(["filename": filename, "idiom": slot.idiom,
                   "size": "\(size)x\(size)", "scale": "\(slot.scale)x"])
}
let contents: [String: Any] = ["images": images, "info": ["author": "xcode", "version": 1]]
let json = try JSONSerialization.data(withJSONObject: contents, options: [.prettyPrinted, .sortedKeys])
try (json + Data([0x0a])).write(to: outputDirectory.appendingPathComponent("Contents.json"))
