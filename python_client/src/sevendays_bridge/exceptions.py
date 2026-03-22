class SevenDaysBridgeError(Exception):
    """Base exception for the Python client."""


class BridgeConnectionError(SevenDaysBridgeError):
    """Raised when the client cannot reach the local bridge."""


class BridgeProtocolError(SevenDaysBridgeError):
    """Raised when the bridge returns malformed data."""


class BridgeApiError(SevenDaysBridgeError):
    """Raised when the bridge returns an application-level error."""

    def __init__(self, error_type: str, message: str):
        super().__init__(f"{error_type}: {message}")
        self.error_type = error_type
        self.message = message
