import AppKit
import Foundation

guard CommandLine.arguments.count == 3 else {
    fatalError("Usage: create-macos-icon.swift SOURCE_IMAGE OUTPUT.png")
}

let sourcePath = CommandLine.arguments[1]
let outputPath = CommandLine.arguments[2]
let size = 1024

guard
    let image = NSImage(contentsOfFile: sourcePath),
    let source = image.cgImage(forProposedRect: nil, context: nil, hints: nil),
    let context = CGContext(
        data: nil,
        width: size,
        height: size,
        bitsPerComponent: 8,
        bytesPerRow: 0,
        space: CGColorSpaceCreateDeviceRGB(),
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
    )
else {
    fatalError("Cannot read image: \(sourcePath)")
}

context.setFillColor(CGColor(red: 23.0 / 255.0, green: 25.0 / 255.0, blue: 28.0 / 255.0, alpha: 1))
context.fill(CGRect(x: 0, y: 0, width: size, height: size))
context.interpolationQuality = .high
context.draw(source, in: CGRect(x: 0, y: 0, width: size, height: size))

guard
    let output = context.makeImage(),
    let data = NSBitmapImageRep(cgImage: output).representation(using: .png, properties: [:])
else {
    fatalError("Cannot create macOS icon")
}

try data.write(to: URL(fileURLWithPath: outputPath))
