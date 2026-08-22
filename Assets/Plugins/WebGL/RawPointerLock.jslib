mergeInto(LibraryManager.library, {
  EnableRawPointerLock: function () {
    var canvas = Module["canvas"];
    if (!canvas || canvas.__lastMagRawPointerLock || !canvas.requestPointerLock) {
      return;
    }

    var requestPointerLock = canvas.requestPointerLock.bind(canvas);
    canvas.__lastMagRawPointerLock = true;
    canvas.requestPointerLock = function () {
      try {
        return requestPointerLock({ unadjustedMovement: true });
      } catch (error) {
        return requestPointerLock();
      }
    };
  }
});
