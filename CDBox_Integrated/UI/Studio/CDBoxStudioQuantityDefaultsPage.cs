using System;
using System.Text;

namespace TCPipeAutoDraw.UI.Studio
{
    internal static class CDBoxStudioQuantityDefaultsPage
    {
        private const string EmbeddedStyleBase64 =
                "OnJvb3R7LS1iZzojZjdmOWZlOy0tcGFuZWw6I2ZmZjstLXBhbmVsMjojZjhmYmZmOy0tbXV0ZWQ6IzY0NzQ4YjstLXRleHQ6IzE2" +
                "MjAzMzstLWxpbmU6I2RjZThmNjstLWxpbmUyOiNlOGVlZjg7LS1icmFuZDojM2I4MmY2Oy0tYnJhbmQyOiM3YzNhZWQ7LS1vazoj" +
                "MTBiOTgxOy0td2FybjojZjU5ZTBiOy0td2Fybi1iZzojZmZmN2VkOy0tZXJyOiNlZjQ0NDQ7LS1lcnItYmc6I2ZmZjFmMjstLXNo" +
                "YWRvdzowIDIycHggNDZweCByZ2JhKDMwLDQxLDU5LC4xMik7LS1zaGFkb3cyOjAgMTJweCAyOHB4IHJnYmEoMzAsNDEsNTksLjA4" +
                "KTstLWlucHV0OiNmZmZmZmZ9CmJvZHlbZGF0YS10aGVtZT0nZnJlc2gnXXstLWJnOiNmM2Y3ZmY7LS1wYW5lbDojZmZmZmZmOy0t" +
                "cGFuZWwyOiNmNWY5ZmY7LS1tdXRlZDojNjU3NThmOy0tdGV4dDojMTcyMDMzOy0tbGluZTojZDdlNmZiOy0tbGluZTI6I2U0ZWZm" +
                "ZjstLWJyYW5kOiMyNTYzZWI7LS1icmFuZDI6IzkzMzNlYTstLWlucHV0OiNmZmZmZmZ9CmJvZHlbZGF0YS10aGVtZT0nZGFyaydd" +
                "ey0tYmc6IzEwMTgyNzstLXBhbmVsOiMxNzIwMzM7LS1wYW5lbDI6IzExMTgyNzstLW11dGVkOiM5NGEzYjg7LS10ZXh0OiNlNWU3" +
                "ZWI7LS1saW5lOiMyNjM2NGQ7LS1saW5lMjojMjMzMDQ0Oy0tYnJhbmQ6IzYwYTVmYTstLWJyYW5kMjojYTc4YmZhOy0td2Fybi1i" +
                "ZzojMmIyMDEyOy0tZXJyLWJnOiMzMTE4MWQ7LS1zaGFkb3c6MCAyMnB4IDQ2cHggcmdiYSgwLDAsMCwuMzApOy0tc2hhZG93Mjow" +
                "IDEycHggMjhweCByZ2JhKDAsMCwwLC4yNCk7LS1pbnB1dDojMTAxODI3fQoucXVhbnRpdHktZGVmYXVsdHMtcGFnZXtjb2xvcjp2" +
                "YXIoLS10ZXh0KX0ucWQtY2FyZHtiYWNrZ3JvdW5kOnZhcigtLXBhbmVsKTtib3JkZXI6MXB4IHNvbGlkIHZhcigtLWxpbmUpO2Jv" +
                "cmRlci1yYWRpdXM6MThweDtib3gtc2hhZG93OnZhcigtLXNoYWRvdzIpO3BhZGRpbmc6MThweDtkaXNwbGF5OmZsZXg7ZmxleC1k" +
                "aXJlY3Rpb246Y29sdW1uO292ZXJmbG93OmhpZGRlbn0ucWQtY2FyZC1oZWFke2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpmbGV4" +
                "LXN0YXJ0O2p1c3RpZnktY29udGVudDpzcGFjZS1iZXR3ZWVuO2dhcDoxOHB4O21hcmdpbi1ib3R0b206MTRweH0ucWQtdGl0bGUg" +
                "c3Ryb25ne2Rpc3BsYXk6YmxvY2s7Zm9udC1zaXplOjIwcHh9LnFkLXRpdGxlIGVte2Rpc3BsYXk6YmxvY2s7Y29sb3I6dmFyKC0t" +
                "bXV0ZWQpO2ZvbnQtc3R5bGU6bm9ybWFsO2ZvbnQtc2l6ZToxMnB4O21hcmdpbi10b3A6NHB4O2xpbmUtaGVpZ2h0OjEuNX0ucWQt" +
                "YWN0aW9uc3tkaXNwbGF5OmZsZXg7Z2FwOjEwcHg7ZmxleC13cmFwOndyYXA7anVzdGlmeS1jb250ZW50OmZsZXgtZW5kfS5xZC1i" +
                "dG57Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lKTtiYWNrZ3JvdW5kOnZhcigtLXBhbmVsKTtib3JkZXItcmFkaXVzOjEycHg7" +
                "cGFkZGluZzo5cHggMTNweDtjdXJzb3I6cG9pbnRlcjtmb250LXdlaWdodDo3MDA7dHJhbnNpdGlvbjpiYWNrZ3JvdW5kIC4xNnMg" +
                "ZWFzZSxib3JkZXItY29sb3IgLjE2cyBlYXNlLHRyYW5zZm9ybSAuMTZzIGVhc2V9LnFkLWJ0bjpob3ZlcntiYWNrZ3JvdW5kOnZh" +
                "cigtLXBhbmVsMik7dHJhbnNmb3JtOnRyYW5zbGF0ZVkoLTFweCl9LnFkLWJ0bi5wcmltYXJ5e2JvcmRlcjowO2NvbG9yOiNmZmY7" +
                "YmFja2dyb3VuZDpsaW5lYXItZ3JhZGllbnQoMTM1ZGVnLHZhcigtLWJyYW5kKSx2YXIoLS1icmFuZDIpKTtib3gtc2hhZG93OjAg" +
                "MTJweCAyNHB4IHJnYmEoNTksMTMwLDI0NiwuMTgpfS5xZC1idG4ud2FybmluZ3tib3JkZXItY29sb3I6I2ZkYmE3NDtiYWNrZ3Jv" +
                "dW5kOnZhcigtLXdhcm4tYmcpO2NvbG9yOiNjMjQxMGN9LnFkLWJ0bi5kYW5nZXJ7Ym9yZGVyLWNvbG9yOiNmZWNhY2E7YmFja2dy" +
                "b3VuZDp2YXIoLS1wYW5lbCk7Y29sb3I6I2I5MWMxY30ucWQtYnRuLmRhbmdlcjpob3ZlcntiYWNrZ3JvdW5kOiNmZWUyZTJ9LnFk" +
                "LWJ0bi5sZWdhY3l7YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpfS5xZC10YWJzLC5wcm9maWxlLXRhYnMucWQtdGFic3tkaXNwbGF5" +
                "OmZsZXg7Z2FwOjEwcHg7Ym9yZGVyLWJvdHRvbToxcHggc29saWQgdmFyKC0tbGluZSk7cGFkZGluZy1ib3R0b206MTJweDttYXJn" +
                "aW4tYm90dG9tOjE0cHh9LnFkLXRhYiwucHJvZmlsZS10YWJ7Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lKTtiYWNrZ3JvdW5k" +
                "OnZhcigtLXBhbmVsMik7Ym9yZGVyLXJhZGl1czo5OTlweDtwYWRkaW5nOjlweCAxNHB4O2N1cnNvcjpwb2ludGVyO2NvbG9yOnZh" +
                "cigtLW11dGVkKTtmb250LXdlaWdodDo4MDB9LnFkLXRhYi5hY3RpdmUsLnByb2ZpbGUtdGFiLmFjdGl2ZXtjb2xvcjojZmZmO2Jv" +
                "cmRlci1jb2xvcjp0cmFuc3BhcmVudDtiYWNrZ3JvdW5kOmxpbmVhci1ncmFkaWVudCgxMzVkZWcsdmFyKC0tYnJhbmQpLHZhcigt" +
                "LWJyYW5kMikpO2JveC1zaGFkb3c6MCAxMnB4IDI0cHggcmdiYSg1OSwxMzAsMjQ2LC4xOCl9LnFkLWVkaXRvciwucHJvZmlsZS1l" +
                "ZGl0b3IucWQtZWRpdG9ye2ZsZXg6MTttaW4taGVpZ2h0OjA7b3ZlcmZsb3c6YXV0bztwYWRkaW5nLXJpZ2h0OjRweH0ucWQtbm90" +
                "ZXtkaXNwbGF5OmZsZXg7YWxpZ24taXRlbXM6Y2VudGVyO2p1c3RpZnktY29udGVudDpzcGFjZS1iZXR3ZWVuO2dhcDoxMnB4O21h" +
                "cmdpbi1ib3R0b206MTRweDtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9udC1zaXplOjEzcHh9LnFkLWZpZWxkc3tkaXNwbGF5OmdyaWQ7" +
                "Z3JpZC10ZW1wbGF0ZS1jb2x1bW5zOnJlcGVhdCgyLG1pbm1heCgwLDFmcikpO2dhcDoxMnB4fS5xZC1maWVsZHtib3JkZXI6MXB4" +
                "IHNvbGlkIHZhcigtLWxpbmUpO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKTtib3JkZXItcmFkaXVzOjE0cHg7cGFkZGluZzoxMnB4" +
                "fS5xZC1maWVsZC53aWRle2dyaWQtY29sdW1uOjEvLTF9LnFkLWZpZWxkIGxhYmVse2Rpc3BsYXk6YmxvY2s7Zm9udC13ZWlnaHQ6" +
                "ODAwO2ZvbnQtc2l6ZToxM3B4O21hcmdpbi1ib3R0b206N3B4fS5xZC1maWVsZCBwe21hcmdpbjo3cHggMCAwO2NvbG9yOnZhcigt" +
                "LW11dGVkKTtmb250LXNpemU6MTJweDtsaW5lLWhlaWdodDoxLjQ1fS5xZC1maWVsZCBpbnB1dFt0eXBlPXRleHRdLC5xZC1maWVs" +
                "ZCBpbnB1dFt0eXBlPW51bWJlcl0sLnFkLWZpZWxkIHNlbGVjdCwucWQtZmllbGQgdGV4dGFyZWEsLmxheWVyLXRhYmxlIGlucHV0" +
                "LC5sYXllci10YWJsZSBzZWxlY3QsLmNvbW1vbi1zZWxlY3R7d2lkdGg6MTAwJTtib3JkZXI6MXB4IHNvbGlkIHZhcigtLWxpbmUy" +
                "KTtib3JkZXItcmFkaXVzOjEwcHg7YmFja2dyb3VuZDp2YXIoLS1pbnB1dCk7Y29sb3I6dmFyKC0tdGV4dCk7cGFkZGluZzo5cHg7" +
                "b3V0bGluZTowfS5xZC1maWVsZCBpbnB1dDpmb2N1cywucWQtZmllbGQgc2VsZWN0OmZvY3VzLC5xZC1maWVsZCB0ZXh0YXJlYTpm" +
                "b2N1cywubGF5ZXItdGFibGUgaW5wdXQ6Zm9jdXMsLmxheWVyLXRhYmxlIHNlbGVjdDpmb2N1cywuY29tbW9uLXNlbGVjdDpmb2N1" +
                "c3tib3JkZXItY29sb3I6cmdiYSg1OSwxMzAsMjQ2LC41NSk7Ym94LXNoYWRvdzowIDAgMCAzcHggcmdiYSg1OSwxMzAsMjQ2LC4x" +
                "MCl9LnFkLWZpZWxkIHRleHRhcmVhe21pbi1oZWlnaHQ6MTUwcHg7cmVzaXplOnZlcnRpY2FsO2xpbmUtaGVpZ2h0OjEuNTV9LmNo" +
                "ZWNrLWZpZWxke2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpjZW50ZXI7Z2FwOjEwcHg7bWluLWhlaWdodDo0MHB4fS5jaGVjay1m" +
                "aWVsZCBpbnB1dHt3aWR0aDoxOHB4O2hlaWdodDoxOHB4O2FjY2VudC1jb2xvcjp2YXIoLS1icmFuZCl9LnN0cnVjdHVyZS1lZGl0" +
                "b3J7ZGlzcGxheTpmbGV4O2ZsZXgtZGlyZWN0aW9uOmNvbHVtbjtnYXA6MTBweH0uc3RydWN0dXJlLWhlYWR7ZGlzcGxheTpmbGV4" +
                "O2FsaWduLWl0ZW1zOmZsZXgtc3RhcnQ7anVzdGlmeS1jb250ZW50OnNwYWNlLWJldHdlZW47Z2FwOjEycHh9LnN0cnVjdHVyZS1o" +
                "ZWFkIHN0cm9uZ3tmb250LXNpemU6MTRweH0uc3RydWN0dXJlLWhlYWQgc3BhbntkaXNwbGF5OmJsb2NrO21hcmdpbi10b3A6NHB4" +
                "O2ZvbnQtc2l6ZToxMnB4O2NvbG9yOnZhcigtLW11dGVkKTtsaW5lLWhlaWdodDoxLjQ1fS5zdHJ1Y3R1cmUtdG9vbGJhcntkaXNw" +
                "bGF5OmZsZXg7YWxpZ24taXRlbXM6Y2VudGVyO2p1c3RpZnktY29udGVudDpzcGFjZS1iZXR3ZWVuO2dhcDoxMHB4O2ZsZXgtd3Jh" +
                "cDp3cmFwO2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7YmFja2dyb3VuZDp2YXIoLS1wYW5lbCk7Ym9yZGVyLXJhZGl1czox" +
                "NHB4O3BhZGRpbmc6MTBweH0udG9vbC1sZWZ0LC50b29sLXJpZ2h0e2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpjZW50ZXI7Z2Fw" +
                "OjhweDtmbGV4LXdyYXA6d3JhcH0uc21hbGwtYnRue2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7YmFja2dyb3VuZDp2YXIo" +
                "LS1wYW5lbDIpO2JvcmRlci1yYWRpdXM6MTBweDtwYWRkaW5nOjdweCAxMHB4O2N1cnNvcjpwb2ludGVyO2ZvbnQtd2VpZ2h0Ojgw" +
                "MDtmb250LXNpemU6MTJweH0uc21hbGwtYnRuOmhvdmVye2JvcmRlci1jb2xvcjpyZ2JhKDU5LDEzMCwyNDYsLjQ1KTtiYWNrZ3Jv" +
                "dW5kOnJnYmEoNTksMTMwLDI0NiwuMDgpfS5zbWFsbC1idG4ud2FybmluZ3tib3JkZXItY29sb3I6I2ZkYmE3NDtiYWNrZ3JvdW5k" +
                "OnZhcigtLXdhcm4tYmcpO2NvbG9yOiNjMjQxMGN9LnNtYWxsLWJ0bi5kYW5nZXJ7Ym9yZGVyLWNvbG9yOiNmZWNhY2E7Y29sb3I6" +
                "I2I5MWMxYztiYWNrZ3JvdW5kOnZhcigtLXBhbmVsKX0uc21hbGwtYnRuLmRhbmdlcjpob3ZlcntiYWNrZ3JvdW5kOiNmZWUyZTJ9" +
                "LmNvbW1vbi1zZWxlY3R7d2lkdGg6MTYwcHg7bWluLXdpZHRoOjE1MHB4O3BhZGRpbmc6N3B4IDlweDtib3JkZXItcmFkaXVzOjEw" +
                "cHg7Zm9udC1zaXplOjEycHh9LmxheWVyLXdyYXB7Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lKTtiYWNrZ3JvdW5kOnZhcigt" +
                "LXBhbmVsKTtib3JkZXItcmFkaXVzOjE2cHg7b3ZlcmZsb3c6YXV0bzttYXgtaGVpZ2h0OjM2MHB4fS5sYXllci10YWJsZXt3aWR0" +
                "aDoxMDAlO2JvcmRlci1jb2xsYXBzZTpzZXBhcmF0ZTtib3JkZXItc3BhY2luZzowO2ZvbnQtc2l6ZToxMnB4O21pbi13aWR0aDo4" +
                "ODBweH0ubGF5ZXItdGFibGUgdGh7cG9zaXRpb246c3RpY2t5O3RvcDowO3otaW5kZXg6MTtiYWNrZ3JvdW5kOnZhcigtLXBhbmVs" +
                "Mik7Y29sb3I6dmFyKC0tbXV0ZWQpO2ZvbnQtd2VpZ2h0OjkwMDt0ZXh0LWFsaWduOmxlZnQ7Ym9yZGVyLWJvdHRvbToxcHggc29s" +
                "aWQgdmFyKC0tbGluZSk7cGFkZGluZzo5cHh9LmxheWVyLXRhYmxlIHRke2JvcmRlci1ib3R0b206MXB4IHNvbGlkIHZhcigtLWxp" +
                "bmUyKTtwYWRkaW5nOjhweDt2ZXJ0aWNhbC1hbGlnbjptaWRkbGV9LmxheWVyLXRhYmxlIHRyOmxhc3QtY2hpbGQgdGR7Ym9yZGVy" +
                "LWJvdHRvbTowfS5sYXllci10YWJsZSB0ci5zZWxlY3RlZCB0ZHtiYWNrZ3JvdW5kOnJnYmEoNTksMTMwLDI0NiwuMDcpfS5sYXll" +
                "ci10YWJsZSAucHJpb3JpdHl7d2lkdGg6NjJweDtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9udC13ZWlnaHQ6OTAwO3RleHQtYWxpZ246" +
                "Y2VudGVyfS5sYXllci10YWJsZSAubmFtZS1jb2x7bWluLXdpZHRoOjI2MHB4fS5sYXllci10YWJsZSAuaGVpZ2h0LWNvbHt3aWR0" +
                "aDoxMzJweH0ubGF5ZXItdGFibGUgLmZsYWctY29se3dpZHRoOjExMnB4O3RleHQtYWxpZ246Y2VudGVyfS5sYXllci10YWJsZSAu" +
                "bWFyay1jb2x7d2lkdGg6MTUwcHh9LmxheWVyLXRhYmxlIC5yb3ctdG9vbHN7d2lkdGg6MTE2cHh9LmxheWVyLXRhYmxlIGlucHV0" +
                "W3R5cGU9Y2hlY2tib3hde3dpZHRoOjE3cHg7aGVpZ2h0OjE3cHg7YWNjZW50LWNvbG9yOnZhcigtLWJyYW5kKX0ubGF5ZXItdGFi" +
                "bGUgaW5wdXQsLmxheWVyLXRhYmxlIHNlbGVjdHtib3JkZXItcmFkaXVzOjhweDtwYWRkaW5nOjdweCA4cHh9LmlubGluZS10b29s" +
                "c3tkaXNwbGF5OmZsZXg7Z2FwOjZweDtqdXN0aWZ5LWNvbnRlbnQ6ZmxleC1lbmR9Lmljb24tYnRue2JvcmRlcjoxcHggc29saWQg" +
                "dmFyKC0tbGluZSk7YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpO2JvcmRlci1yYWRpdXM6OHB4O3dpZHRoOjMwcHg7aGVpZ2h0OjMw" +
                "cHg7Y3Vyc29yOnBvaW50ZXI7Zm9udC13ZWlnaHQ6OTAwfS5pY29uLWJ0bjpob3Zlcntib3JkZXItY29sb3I6cmdiYSg1OSwxMzAs" +
                "MjQ2LC40NSk7YmFja2dyb3VuZDpyZ2JhKDU5LDEzMCwyNDYsLjA4KX0uaWNvbi1idG4uZGFuZ2Vye2NvbG9yOiNiOTFjMWM7Ym9y" +
                "ZGVyLWNvbG9yOiNmZWNhY2E7YmFja2dyb3VuZDp2YXIoLS1wYW5lbCl9Lmljb24tYnRuLmRhbmdlcjpob3ZlcntiYWNrZ3JvdW5k" +
                "OiNmZWUyZTJ9LmVtcHR5LWxheWVye3BhZGRpbmc6MThweDt0ZXh0LWFsaWduOmNlbnRlcjtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9u" +
                "dC1zaXplOjEzcHh9LnN0cnVjdHVyZS1wcmV2aWV3e2JvcmRlcjoxcHggZGFzaGVkIHZhcigtLWxpbmUpO2JhY2tncm91bmQ6dmFy" +
                "KC0tcGFuZWwyKTtib3JkZXItcmFkaXVzOjE0cHg7cGFkZGluZzoxMHB4O2NvbG9yOnZhcigtLW11dGVkKTtmb250LXNpemU6MTJw" +
                "eDtsaW5lLWhlaWdodDoxLjY7d2hpdGUtc3BhY2U6cHJlLXdyYXA7bWluLWhlaWdodDo0MnB4fS5xZC1oZWxwLC5wcm9maWxlLWhl" +
                "bHAucWQtaGVscHttYXJnaW4tdG9wOjE0cHg7Ym9yZGVyOjFweCBkYXNoZWQgdmFyKC0tbGluZSk7Ym9yZGVyLXJhZGl1czoxNHB4" +
                "O2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKTtwYWRkaW5nOjEycHg7Y29sb3I6dmFyKC0tbXV0ZWQpO2ZvbnQtc2l6ZToxMnB4O2xp" +
                "bmUtaGVpZ2h0OjEuNjV9LnFkLWhlbHAgc3Ryb25ne2NvbG9yOnZhcigtLXRleHQpO21hcmdpbi1yaWdodDo2cHh9LnFkLXBhdGh7" +
                "Zm9udC1zaXplOjEycHg7Y29sb3I6dmFyKC0tbXV0ZWQpO3dvcmQtYnJlYWs6YnJlYWstYWxsO21hcmdpbi10b3A6MTBweH0ucWQt" +
                "ZXJyb3J7aGVpZ2h0OjEwMCU7ZGlzcGxheTpncmlkO3BsYWNlLWl0ZW1zOmNlbnRlcjtwYWRkaW5nOjMwcHh9LnFkLWVycm9yLWNh" +
                "cmR7bWF4LXdpZHRoOjcyMHB4O2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7Ym9yZGVyLXJhZGl1czoxOHB4O2JhY2tncm91" +
                "bmQ6dmFyKC0tcGFuZWwpO2JveC1zaGFkb3c6dmFyKC0tc2hhZG93Mik7cGFkZGluZzoyMnB4fS5xZC1lcnJvci1jYXJkIGgye21h" +
                "cmdpbjowIDAgOHB4fS5xZC1lcnJvci1jYXJkIHByZXt3aGl0ZS1zcGFjZTpwcmUtd3JhcDt3b3JkLWJyZWFrOmJyZWFrLXdvcmQ7" +
                "YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpO2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7Ym9yZGVyLXJhZGl1czoxMnB4O3Bh" +
                "ZGRpbmc6MTJweDtjb2xvcjp2YXIoLS1tdXRlZCl9LnRvYXN0LXN0YWNre3Bvc2l0aW9uOmZpeGVkO3JpZ2h0OjIycHg7dG9wOjE4" +
                "cHg7ei1pbmRleDo1MDtkaXNwbGF5OmZsZXg7ZmxleC1kaXJlY3Rpb246Y29sdW1uO2dhcDoxMHB4fS50b2FzdHttaW4td2lkdGg6" +
                "MjMwcHg7bWF4LXdpZHRoOjM4MHB4O2JhY2tncm91bmQ6dmFyKC0tcGFuZWwpO2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7" +
                "Ym9yZGVyLWxlZnQ6NHB4IHNvbGlkIHZhcigtLWJyYW5kKTtib3JkZXItcmFkaXVzOjE2cHg7cGFkZGluZzoxMnB4IDE0cHg7Ym94" +
                "LXNoYWRvdzp2YXIoLS1zaGFkb3cyKTtmb250LXNpemU6MTNweDthbmltYXRpb246dG9hc3RJbiAuMnMgZWFzZSBib3RofS50b2Fz" +
                "dC5zdWNjZXNze2JvcmRlci1sZWZ0LWNvbG9yOnZhcigtLW9rKX0udG9hc3Qud2FybmluZ3tib3JkZXItbGVmdC1jb2xvcjp2YXIo" +
                "LS13YXJuKX0udG9hc3QuZXJyb3J7Ym9yZGVyLWxlZnQtY29sb3I6dmFyKC0tZXJyKX0ubm8tYW5pbWF0aW9ucyAqLC5uby1hbmlt" +
                "YXRpb25zICo6YmVmb3JlLC5uby1hbmltYXRpb25zICo6YWZ0ZXJ7YW5pbWF0aW9uOm5vbmUhaW1wb3J0YW50O3RyYW5zaXRpb246" +
                "bm9uZSFpbXBvcnRhbnR9QGtleWZyYW1lcyB0b2FzdElue2Zyb217b3BhY2l0eTowO3RyYW5zZm9ybTp0cmFuc2xhdGVYKDEycHgp" +
                "fXRve29wYWNpdHk6MTt0cmFuc2Zvcm06bm9uZX19QG1lZGlhKG1heC13aWR0aDoxMDgwcHgpey5xZC1maWVsZHN7Z3JpZC10ZW1w" +
                "bGF0ZS1jb2x1bW5zOjFmcn0ucWQtY2FyZC1oZWFkLC5zdHJ1Y3R1cmUtdG9vbGJhcntmbGV4LWRpcmVjdGlvbjpjb2x1bW47YWxp" +
                "Z24taXRlbXM6c3RyZXRjaH0ucWQtYWN0aW9uc3tqdXN0aWZ5LWNvbnRlbnQ6ZmxleC1zdGFydH19Cg==";

        private const string StandaloneStyleBase64 =
                "Kntib3gtc2l6aW5nOmJvcmRlci1ib3h9aHRtbCxib2R5e3dpZHRoOjEwMCU7aGVpZ2h0OjEwMCU7bWFyZ2luOjA7YmFja2dyb3Vu" +
                "ZDp0cmFuc3BhcmVudDtmb250LWZhbWlseTonTWljcm9zb2Z0IFlhSGVpIFVJJywnU2Vnb2UgVUknLHN5c3RlbS11aSxzYW5zLXNl" +
                "cmlmO292ZXJmbG93OmhpZGRlbn1idXR0b24saW5wdXQsc2VsZWN0LHRleHRhcmVhe2ZvbnQ6aW5oZXJpdH1idXR0b257Y29sb3I6" +
                "aW5oZXJpdH0ucXVhbnRpdHktZGVmYXVsdHMtc3RhbmRhbG9uZXtoZWlnaHQ6MTAwdmg7YmFja2dyb3VuZDp2YXIoLS1iZyk7b3Zl" +
                "cmZsb3c6aGlkZGVufS5xZC1wYWdle2hlaWdodDoxMDAlO3BhZGRpbmc6MThweCAyMHB4IDIwcHh9LnF1YW50aXR5LWRlZmF1bHRz" +
                "LXN0YW5kYWxvbmUgLnFkLWNhcmR7aGVpZ2h0OjEwMCV9Cjpyb290ey0tYmc6I2Y3ZjlmZTstLXBhbmVsOiNmZmY7LS1wYW5lbDI6" +
                "I2Y4ZmJmZjstLW11dGVkOiM2NDc0OGI7LS10ZXh0OiMxNjIwMzM7LS1saW5lOiNkY2U4ZjY7LS1saW5lMjojZThlZWY4Oy0tYnJh" +
                "bmQ6IzNiODJmNjstLWJyYW5kMjojN2MzYWVkOy0tb2s6IzEwYjk4MTstLXdhcm46I2Y1OWUwYjstLXdhcm4tYmc6I2ZmZjdlZDst" +
                "LWVycjojZWY0NDQ0Oy0tZXJyLWJnOiNmZmYxZjI7LS1zaGFkb3c6MCAyMnB4IDQ2cHggcmdiYSgzMCw0MSw1OSwuMTIpOy0tc2hh" +
                "ZG93MjowIDEycHggMjhweCByZ2JhKDMwLDQxLDU5LC4wOCk7LS1pbnB1dDojZmZmZmZmfQpib2R5W2RhdGEtdGhlbWU9J2ZyZXNo" +
                "J117LS1iZzojZjNmN2ZmOy0tcGFuZWw6I2ZmZmZmZjstLXBhbmVsMjojZjVmOWZmOy0tbXV0ZWQ6IzY1NzU4ZjstLXRleHQ6IzE3" +
                "MjAzMzstLWxpbmU6I2Q3ZTZmYjstLWxpbmUyOiNlNGVmZmY7LS1icmFuZDojMjU2M2ViOy0tYnJhbmQyOiM5MzMzZWE7LS1pbnB1" +
                "dDojZmZmZmZmfQpib2R5W2RhdGEtdGhlbWU9J2RhcmsnXXstLWJnOiMxMDE4Mjc7LS1wYW5lbDojMTcyMDMzOy0tcGFuZWwyOiMx" +
                "MTE4Mjc7LS1tdXRlZDojOTRhM2I4Oy0tdGV4dDojZTVlN2ViOy0tbGluZTojMjYzNjRkOy0tbGluZTI6IzIzMzA0NDstLWJyYW5k" +
                "OiM2MGE1ZmE7LS1icmFuZDI6I2E3OGJmYTstLXdhcm4tYmc6IzJiMjAxMjstLWVyci1iZzojMzExODFkOy0tc2hhZG93OjAgMjJw" +
                "eCA0NnB4IHJnYmEoMCwwLDAsLjMwKTstLXNoYWRvdzI6MCAxMnB4IDI4cHggcmdiYSgwLDAsMCwuMjQpOy0taW5wdXQ6IzEwMTgy" +
                "N30KLnF1YW50aXR5LWRlZmF1bHRzLXBhZ2V7Y29sb3I6dmFyKC0tdGV4dCl9LnFkLWNhcmR7YmFja2dyb3VuZDp2YXIoLS1wYW5l" +
                "bCk7Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lKTtib3JkZXItcmFkaXVzOjE4cHg7Ym94LXNoYWRvdzp2YXIoLS1zaGFkb3cy" +
                "KTtwYWRkaW5nOjE4cHg7ZGlzcGxheTpmbGV4O2ZsZXgtZGlyZWN0aW9uOmNvbHVtbjtvdmVyZmxvdzpoaWRkZW59LnFkLWNhcmQt" +
                "aGVhZHtkaXNwbGF5OmZsZXg7YWxpZ24taXRlbXM6ZmxleC1zdGFydDtqdXN0aWZ5LWNvbnRlbnQ6c3BhY2UtYmV0d2VlbjtnYXA6" +
                "MThweDttYXJnaW4tYm90dG9tOjE0cHh9LnFkLXRpdGxlIHN0cm9uZ3tkaXNwbGF5OmJsb2NrO2ZvbnQtc2l6ZToyMHB4fS5xZC10" +
                "aXRsZSBlbXtkaXNwbGF5OmJsb2NrO2NvbG9yOnZhcigtLW11dGVkKTtmb250LXN0eWxlOm5vcm1hbDtmb250LXNpemU6MTJweDtt" +
                "YXJnaW4tdG9wOjRweDtsaW5lLWhlaWdodDoxLjV9LnFkLWFjdGlvbnN7ZGlzcGxheTpmbGV4O2dhcDoxMHB4O2ZsZXgtd3JhcDp3" +
                "cmFwO2p1c3RpZnktY29udGVudDpmbGV4LWVuZH0ucWQtYnRue2JvcmRlcjoxcHggc29saWQgdmFyKC0tbGluZSk7YmFja2dyb3Vu" +
                "ZDp2YXIoLS1wYW5lbCk7Ym9yZGVyLXJhZGl1czoxMnB4O3BhZGRpbmc6OXB4IDEzcHg7Y3Vyc29yOnBvaW50ZXI7Zm9udC13ZWln" +
                "aHQ6NzAwO3RyYW5zaXRpb246YmFja2dyb3VuZCAuMTZzIGVhc2UsYm9yZGVyLWNvbG9yIC4xNnMgZWFzZSx0cmFuc2Zvcm0gLjE2" +
                "cyBlYXNlfS5xZC1idG46aG92ZXJ7YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpO3RyYW5zZm9ybTp0cmFuc2xhdGVZKC0xcHgpfS5x" +
                "ZC1idG4ucHJpbWFyeXtib3JkZXI6MDtjb2xvcjojZmZmO2JhY2tncm91bmQ6bGluZWFyLWdyYWRpZW50KDEzNWRlZyx2YXIoLS1i" +
                "cmFuZCksdmFyKC0tYnJhbmQyKSk7Ym94LXNoYWRvdzowIDEycHggMjRweCByZ2JhKDU5LDEzMCwyNDYsLjE4KX0ucWQtYnRuLndh" +
                "cm5pbmd7Ym9yZGVyLWNvbG9yOiNmZGJhNzQ7YmFja2dyb3VuZDp2YXIoLS13YXJuLWJnKTtjb2xvcjojYzI0MTBjfS5xZC1idG4u" +
                "ZGFuZ2Vye2JvcmRlci1jb2xvcjojZmVjYWNhO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwpO2NvbG9yOiNiOTFjMWN9LnFkLWJ0bi5k" +
                "YW5nZXI6aG92ZXJ7YmFja2dyb3VuZDojZmVlMmUyfS5xZC1idG4ubGVnYWN5e2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKX0ucWQt" +
                "dGFicywucHJvZmlsZS10YWJzLnFkLXRhYnN7ZGlzcGxheTpmbGV4O2dhcDoxMHB4O2JvcmRlci1ib3R0b206MXB4IHNvbGlkIHZh" +
                "cigtLWxpbmUpO3BhZGRpbmctYm90dG9tOjEycHg7bWFyZ2luLWJvdHRvbToxNHB4fS5xZC10YWIsLnByb2ZpbGUtdGFie2JvcmRl" +
                "cjoxcHggc29saWQgdmFyKC0tbGluZSk7YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpO2JvcmRlci1yYWRpdXM6OTk5cHg7cGFkZGlu" +
                "Zzo5cHggMTRweDtjdXJzb3I6cG9pbnRlcjtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9udC13ZWlnaHQ6ODAwfS5xZC10YWIuYWN0aXZl" +
                "LC5wcm9maWxlLXRhYi5hY3RpdmV7Y29sb3I6I2ZmZjtib3JkZXItY29sb3I6dHJhbnNwYXJlbnQ7YmFja2dyb3VuZDpsaW5lYXIt" +
                "Z3JhZGllbnQoMTM1ZGVnLHZhcigtLWJyYW5kKSx2YXIoLS1icmFuZDIpKTtib3gtc2hhZG93OjAgMTJweCAyNHB4IHJnYmEoNTks" +
                "MTMwLDI0NiwuMTgpfS5xZC1lZGl0b3IsLnByb2ZpbGUtZWRpdG9yLnFkLWVkaXRvcntmbGV4OjE7bWluLWhlaWdodDowO292ZXJm" +
                "bG93OmF1dG87cGFkZGluZy1yaWdodDo0cHh9LnFkLW5vdGV7ZGlzcGxheTpmbGV4O2FsaWduLWl0ZW1zOmNlbnRlcjtqdXN0aWZ5" +
                "LWNvbnRlbnQ6c3BhY2UtYmV0d2VlbjtnYXA6MTJweDttYXJnaW4tYm90dG9tOjE0cHg7Y29sb3I6dmFyKC0tbXV0ZWQpO2ZvbnQt" +
                "c2l6ZToxM3B4fS5xZC1maWVsZHN7ZGlzcGxheTpncmlkO2dyaWQtdGVtcGxhdGUtY29sdW1uczpyZXBlYXQoMixtaW5tYXgoMCwx" +
                "ZnIpKTtnYXA6MTJweH0ucWQtZmllbGR7Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lKTtiYWNrZ3JvdW5kOnZhcigtLXBhbmVs" +
                "Mik7Ym9yZGVyLXJhZGl1czoxNHB4O3BhZGRpbmc6MTJweH0ucWQtZmllbGQud2lkZXtncmlkLWNvbHVtbjoxLy0xfS5xZC1maWVs" +
                "ZCBsYWJlbHtkaXNwbGF5OmJsb2NrO2ZvbnQtd2VpZ2h0OjgwMDtmb250LXNpemU6MTNweDttYXJnaW4tYm90dG9tOjdweH0ucWQt" +
                "ZmllbGQgcHttYXJnaW46N3B4IDAgMDtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9udC1zaXplOjEycHg7bGluZS1oZWlnaHQ6MS40NX0u" +
                "cWQtZmllbGQgaW5wdXRbdHlwZT10ZXh0XSwucWQtZmllbGQgaW5wdXRbdHlwZT1udW1iZXJdLC5xZC1maWVsZCBzZWxlY3QsLnFk" +
                "LWZpZWxkIHRleHRhcmVhLC5sYXllci10YWJsZSBpbnB1dCwubGF5ZXItdGFibGUgc2VsZWN0LC5jb21tb24tc2VsZWN0e3dpZHRo" +
                "OjEwMCU7Ym9yZGVyOjFweCBzb2xpZCB2YXIoLS1saW5lMik7Ym9yZGVyLXJhZGl1czoxMHB4O2JhY2tncm91bmQ6dmFyKC0taW5w" +
                "dXQpO2NvbG9yOnZhcigtLXRleHQpO3BhZGRpbmc6OXB4O291dGxpbmU6MH0ucWQtZmllbGQgaW5wdXQ6Zm9jdXMsLnFkLWZpZWxk" +
                "IHNlbGVjdDpmb2N1cywucWQtZmllbGQgdGV4dGFyZWE6Zm9jdXMsLmxheWVyLXRhYmxlIGlucHV0OmZvY3VzLC5sYXllci10YWJs" +
                "ZSBzZWxlY3Q6Zm9jdXMsLmNvbW1vbi1zZWxlY3Q6Zm9jdXN7Ym9yZGVyLWNvbG9yOnJnYmEoNTksMTMwLDI0NiwuNTUpO2JveC1z" +
                "aGFkb3c6MCAwIDAgM3B4IHJnYmEoNTksMTMwLDI0NiwuMTApfS5xZC1maWVsZCB0ZXh0YXJlYXttaW4taGVpZ2h0OjE1MHB4O3Jl" +
                "c2l6ZTp2ZXJ0aWNhbDtsaW5lLWhlaWdodDoxLjU1fS5jaGVjay1maWVsZHtkaXNwbGF5OmZsZXg7YWxpZ24taXRlbXM6Y2VudGVy" +
                "O2dhcDoxMHB4O21pbi1oZWlnaHQ6NDBweH0uY2hlY2stZmllbGQgaW5wdXR7d2lkdGg6MThweDtoZWlnaHQ6MThweDthY2NlbnQt" +
                "Y29sb3I6dmFyKC0tYnJhbmQpfS5zdHJ1Y3R1cmUtZWRpdG9ye2Rpc3BsYXk6ZmxleDtmbGV4LWRpcmVjdGlvbjpjb2x1bW47Z2Fw" +
                "OjEwcHh9LnN0cnVjdHVyZS1oZWFke2Rpc3BsYXk6ZmxleDthbGlnbi1pdGVtczpmbGV4LXN0YXJ0O2p1c3RpZnktY29udGVudDpz" +
                "cGFjZS1iZXR3ZWVuO2dhcDoxMnB4fS5zdHJ1Y3R1cmUtaGVhZCBzdHJvbmd7Zm9udC1zaXplOjE0cHh9LnN0cnVjdHVyZS1oZWFk" +
                "IHNwYW57ZGlzcGxheTpibG9jazttYXJnaW4tdG9wOjRweDtmb250LXNpemU6MTJweDtjb2xvcjp2YXIoLS1tdXRlZCk7bGluZS1o" +
                "ZWlnaHQ6MS40NX0uc3RydWN0dXJlLXRvb2xiYXJ7ZGlzcGxheTpmbGV4O2FsaWduLWl0ZW1zOmNlbnRlcjtqdXN0aWZ5LWNvbnRl" +
                "bnQ6c3BhY2UtYmV0d2VlbjtnYXA6MTBweDtmbGV4LXdyYXA6d3JhcDtib3JkZXI6MXB4IHNvbGlkIHZhcigtLWxpbmUpO2JhY2tn" +
                "cm91bmQ6dmFyKC0tcGFuZWwpO2JvcmRlci1yYWRpdXM6MTRweDtwYWRkaW5nOjEwcHh9LnRvb2wtbGVmdCwudG9vbC1yaWdodHtk" +
                "aXNwbGF5OmZsZXg7YWxpZ24taXRlbXM6Y2VudGVyO2dhcDo4cHg7ZmxleC13cmFwOndyYXB9LnNtYWxsLWJ0bntib3JkZXI6MXB4" +
                "IHNvbGlkIHZhcigtLWxpbmUpO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKTtib3JkZXItcmFkaXVzOjEwcHg7cGFkZGluZzo3cHgg" +
                "MTBweDtjdXJzb3I6cG9pbnRlcjtmb250LXdlaWdodDo4MDA7Zm9udC1zaXplOjEycHh9LnNtYWxsLWJ0bjpob3Zlcntib3JkZXIt" +
                "Y29sb3I6cmdiYSg1OSwxMzAsMjQ2LC40NSk7YmFja2dyb3VuZDpyZ2JhKDU5LDEzMCwyNDYsLjA4KX0uc21hbGwtYnRuLndhcm5p" +
                "bmd7Ym9yZGVyLWNvbG9yOiNmZGJhNzQ7YmFja2dyb3VuZDp2YXIoLS13YXJuLWJnKTtjb2xvcjojYzI0MTBjfS5zbWFsbC1idG4u" +
                "ZGFuZ2Vye2JvcmRlci1jb2xvcjojZmVjYWNhO2NvbG9yOiNiOTFjMWM7YmFja2dyb3VuZDp2YXIoLS1wYW5lbCl9LnNtYWxsLWJ0" +
                "bi5kYW5nZXI6aG92ZXJ7YmFja2dyb3VuZDojZmVlMmUyfS5jb21tb24tc2VsZWN0e3dpZHRoOjE2MHB4O21pbi13aWR0aDoxNTBw" +
                "eDtwYWRkaW5nOjdweCA5cHg7Ym9yZGVyLXJhZGl1czoxMHB4O2ZvbnQtc2l6ZToxMnB4fS5sYXllci13cmFwe2JvcmRlcjoxcHgg" +
                "c29saWQgdmFyKC0tbGluZSk7YmFja2dyb3VuZDp2YXIoLS1wYW5lbCk7Ym9yZGVyLXJhZGl1czoxNnB4O292ZXJmbG93OmF1dG87" +
                "bWF4LWhlaWdodDozNjBweH0ubGF5ZXItdGFibGV7d2lkdGg6MTAwJTtib3JkZXItY29sbGFwc2U6c2VwYXJhdGU7Ym9yZGVyLXNw" +
                "YWNpbmc6MDtmb250LXNpemU6MTJweDttaW4td2lkdGg6ODgwcHh9LmxheWVyLXRhYmxlIHRoe3Bvc2l0aW9uOnN0aWNreTt0b3A6" +
                "MDt6LWluZGV4OjE7YmFja2dyb3VuZDp2YXIoLS1wYW5lbDIpO2NvbG9yOnZhcigtLW11dGVkKTtmb250LXdlaWdodDo5MDA7dGV4" +
                "dC1hbGlnbjpsZWZ0O2JvcmRlci1ib3R0b206MXB4IHNvbGlkIHZhcigtLWxpbmUpO3BhZGRpbmc6OXB4fS5sYXllci10YWJsZSB0" +
                "ZHtib3JkZXItYm90dG9tOjFweCBzb2xpZCB2YXIoLS1saW5lMik7cGFkZGluZzo4cHg7dmVydGljYWwtYWxpZ246bWlkZGxlfS5s" +
                "YXllci10YWJsZSB0cjpsYXN0LWNoaWxkIHRke2JvcmRlci1ib3R0b206MH0ubGF5ZXItdGFibGUgdHIuc2VsZWN0ZWQgdGR7YmFj" +
                "a2dyb3VuZDpyZ2JhKDU5LDEzMCwyNDYsLjA3KX0ubGF5ZXItdGFibGUgLnByaW9yaXR5e3dpZHRoOjYycHg7Y29sb3I6dmFyKC0t" +
                "bXV0ZWQpO2ZvbnQtd2VpZ2h0OjkwMDt0ZXh0LWFsaWduOmNlbnRlcn0ubGF5ZXItdGFibGUgLm5hbWUtY29se21pbi13aWR0aDoy" +
                "NjBweH0ubGF5ZXItdGFibGUgLmhlaWdodC1jb2x7d2lkdGg6MTMycHh9LmxheWVyLXRhYmxlIC5mbGFnLWNvbHt3aWR0aDoxMTJw" +
                "eDt0ZXh0LWFsaWduOmNlbnRlcn0ubGF5ZXItdGFibGUgLm1hcmstY29se3dpZHRoOjE1MHB4fS5sYXllci10YWJsZSAucm93LXRv" +
                "b2xze3dpZHRoOjExNnB4fS5sYXllci10YWJsZSBpbnB1dFt0eXBlPWNoZWNrYm94XXt3aWR0aDoxN3B4O2hlaWdodDoxN3B4O2Fj" +
                "Y2VudC1jb2xvcjp2YXIoLS1icmFuZCl9LmxheWVyLXRhYmxlIGlucHV0LC5sYXllci10YWJsZSBzZWxlY3R7Ym9yZGVyLXJhZGl1" +
                "czo4cHg7cGFkZGluZzo3cHggOHB4fS5pbmxpbmUtdG9vbHN7ZGlzcGxheTpmbGV4O2dhcDo2cHg7anVzdGlmeS1jb250ZW50OmZs" +
                "ZXgtZW5kfS5pY29uLWJ0bntib3JkZXI6MXB4IHNvbGlkIHZhcigtLWxpbmUpO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKTtib3Jk" +
                "ZXItcmFkaXVzOjhweDt3aWR0aDozMHB4O2hlaWdodDozMHB4O2N1cnNvcjpwb2ludGVyO2ZvbnQtd2VpZ2h0OjkwMH0uaWNvbi1i" +
                "dG46aG92ZXJ7Ym9yZGVyLWNvbG9yOnJnYmEoNTksMTMwLDI0NiwuNDUpO2JhY2tncm91bmQ6cmdiYSg1OSwxMzAsMjQ2LC4wOCl9" +
                "Lmljb24tYnRuLmRhbmdlcntjb2xvcjojYjkxYzFjO2JvcmRlci1jb2xvcjojZmVjYWNhO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwp" +
                "fS5pY29uLWJ0bi5kYW5nZXI6aG92ZXJ7YmFja2dyb3VuZDojZmVlMmUyfS5lbXB0eS1sYXllcntwYWRkaW5nOjE4cHg7dGV4dC1h" +
                "bGlnbjpjZW50ZXI7Y29sb3I6dmFyKC0tbXV0ZWQpO2ZvbnQtc2l6ZToxM3B4fS5zdHJ1Y3R1cmUtcHJldmlld3tib3JkZXI6MXB4" +
                "IGRhc2hlZCB2YXIoLS1saW5lKTtiYWNrZ3JvdW5kOnZhcigtLXBhbmVsMik7Ym9yZGVyLXJhZGl1czoxNHB4O3BhZGRpbmc6MTBw" +
                "eDtjb2xvcjp2YXIoLS1tdXRlZCk7Zm9udC1zaXplOjEycHg7bGluZS1oZWlnaHQ6MS42O3doaXRlLXNwYWNlOnByZS13cmFwO21p" +
                "bi1oZWlnaHQ6NDJweH0ucWQtaGVscCwucHJvZmlsZS1oZWxwLnFkLWhlbHB7bWFyZ2luLXRvcDoxNHB4O2JvcmRlcjoxcHggZGFz" +
                "aGVkIHZhcigtLWxpbmUpO2JvcmRlci1yYWRpdXM6MTRweDtiYWNrZ3JvdW5kOnZhcigtLXBhbmVsMik7cGFkZGluZzoxMnB4O2Nv" +
                "bG9yOnZhcigtLW11dGVkKTtmb250LXNpemU6MTJweDtsaW5lLWhlaWdodDoxLjY1fS5xZC1oZWxwIHN0cm9uZ3tjb2xvcjp2YXIo" +
                "LS10ZXh0KTttYXJnaW4tcmlnaHQ6NnB4fS5xZC1wYXRoe2ZvbnQtc2l6ZToxMnB4O2NvbG9yOnZhcigtLW11dGVkKTt3b3JkLWJy" +
                "ZWFrOmJyZWFrLWFsbDttYXJnaW4tdG9wOjEwcHh9LnFkLWVycm9ye2hlaWdodDoxMDAlO2Rpc3BsYXk6Z3JpZDtwbGFjZS1pdGVt" +
                "czpjZW50ZXI7cGFkZGluZzozMHB4fS5xZC1lcnJvci1jYXJke21heC13aWR0aDo3MjBweDtib3JkZXI6MXB4IHNvbGlkIHZhcigt" +
                "LWxpbmUpO2JvcmRlci1yYWRpdXM6MThweDtiYWNrZ3JvdW5kOnZhcigtLXBhbmVsKTtib3gtc2hhZG93OnZhcigtLXNoYWRvdzIp" +
                "O3BhZGRpbmc6MjJweH0ucWQtZXJyb3ItY2FyZCBoMnttYXJnaW46MCAwIDhweH0ucWQtZXJyb3ItY2FyZCBwcmV7d2hpdGUtc3Bh" +
                "Y2U6cHJlLXdyYXA7d29yZC1icmVhazpicmVhay13b3JkO2JhY2tncm91bmQ6dmFyKC0tcGFuZWwyKTtib3JkZXI6MXB4IHNvbGlk" +
                "IHZhcigtLWxpbmUpO2JvcmRlci1yYWRpdXM6MTJweDtwYWRkaW5nOjEycHg7Y29sb3I6dmFyKC0tbXV0ZWQpfS50b2FzdC1zdGFj" +
                "a3twb3NpdGlvbjpmaXhlZDtyaWdodDoyMnB4O3RvcDoxOHB4O3otaW5kZXg6NTA7ZGlzcGxheTpmbGV4O2ZsZXgtZGlyZWN0aW9u" +
                "OmNvbHVtbjtnYXA6MTBweH0udG9hc3R7bWluLXdpZHRoOjIzMHB4O21heC13aWR0aDozODBweDtiYWNrZ3JvdW5kOnZhcigtLXBh" +
                "bmVsKTtib3JkZXI6MXB4IHNvbGlkIHZhcigtLWxpbmUpO2JvcmRlci1sZWZ0OjRweCBzb2xpZCB2YXIoLS1icmFuZCk7Ym9yZGVy" +
                "LXJhZGl1czoxNnB4O3BhZGRpbmc6MTJweCAxNHB4O2JveC1zaGFkb3c6dmFyKC0tc2hhZG93Mik7Zm9udC1zaXplOjEzcHg7YW5p" +
                "bWF0aW9uOnRvYXN0SW4gLjJzIGVhc2UgYm90aH0udG9hc3Quc3VjY2Vzc3tib3JkZXItbGVmdC1jb2xvcjp2YXIoLS1vayl9LnRv" +
                "YXN0Lndhcm5pbmd7Ym9yZGVyLWxlZnQtY29sb3I6dmFyKC0td2Fybil9LnRvYXN0LmVycm9ye2JvcmRlci1sZWZ0LWNvbG9yOnZh" +
                "cigtLWVycil9Lm5vLWFuaW1hdGlvbnMgKiwubm8tYW5pbWF0aW9ucyAqOmJlZm9yZSwubm8tYW5pbWF0aW9ucyAqOmFmdGVye2Fu" +
                "aW1hdGlvbjpub25lIWltcG9ydGFudDt0cmFuc2l0aW9uOm5vbmUhaW1wb3J0YW50fUBrZXlmcmFtZXMgdG9hc3RJbntmcm9te29w" +
                "YWNpdHk6MDt0cmFuc2Zvcm06dHJhbnNsYXRlWCgxMnB4KX10b3tvcGFjaXR5OjE7dHJhbnNmb3JtOm5vbmV9fUBtZWRpYShtYXgt" +
                "d2lkdGg6MTA4MHB4KXsucWQtZmllbGRze2dyaWQtdGVtcGxhdGUtY29sdW1uczoxZnJ9LnFkLWNhcmQtaGVhZCwuc3RydWN0dXJl" +
                "LXRvb2xiYXJ7ZmxleC1kaXJlY3Rpb246Y29sdW1uO2FsaWduLWl0ZW1zOnN0cmV0Y2h9LnFkLWFjdGlvbnN7anVzdGlmeS1jb250" +
                "ZW50OmZsZXgtc3RhcnR9fQo=";

        private const string EmbeddedBridgeScriptBase64 =
                "CihmdW5jdGlvbih3KXsKICBmdW5jdGlvbiBlc2ModmFsdWUpe3JldHVybiBTdHJpbmcodmFsdWU9PW51bGw/Jyc6dmFsdWUpLnJl" +
                "cGxhY2UoL1smPD4iJ10vZyxmdW5jdGlvbihjKXtyZXR1cm4geycmJzonJmFtcDsnLCc8JzonJmx0OycsJz4nOicmZ3Q7JywnIic6" +
                "JyZxdW90OycsIiciOicmIzM5Oyd9W2NdO30pO30KICBmdW5jdGlvbiBjbG9uZVByb2ZpbGVzKHNvdXJjZSl7cmV0dXJuIChzb3Vy" +
                "Y2V8fFtdKS5tYXAoZnVuY3Rpb24ocCl7cmV0dXJuIHtpZDpwLmlkfHwnJyx0aXRsZTpwLnRpdGxlfHwnJyxraW5kOnAua2luZHx8" +
                "JycsZmllbGRzOihwLmZpZWxkc3x8W10pLm1hcChmdW5jdGlvbihmKXtyZXR1cm4ge2tleTpmLmtleXx8JycsbGFiZWw6Zi5sYWJl" +
                "bHx8JycsdHlwZTpmLnR5cGV8fCd0ZXh0Jyx2YWx1ZTpmLnZhbHVlfHwnJyx3aWRlOiEhZi53aWRlLGhlbHA6Zi5oZWxwfHwnJyxv" +
                "cHRpb25zOihmLm9wdGlvbnN8fFtdKS5zbGljZSgwKX07fSl9O30pO30KICBmdW5jdGlvbiBlbmNvZGU2NCh2YWx1ZSl7cmV0dXJu" +
                "IGJ0b2EodW5lc2NhcGUoZW5jb2RlVVJJQ29tcG9uZW50KHZhbHVlfHwnJykpKTt9CiAgdmFyIGNvbW1vbkxheWVycz1be25hbWU6" +
                "J0MyNeegvOaBouWkjScsaGVpZ2h0OicwLjI1Jyxsb2NrZWQ6dHJ1ZSxtYXJrOicnfSx7bmFtZTon56KO55+z5Z6r5bGCJyxoZWln" +
                "aHQ6JzAuMTAnLGxvY2tlZDp0cnVlLG1hcms6Jyd9LHtuYW1lOifkuK3nspfnoILlm57loasnLGhlaWdodDonMC44MCcsbG9ja2Vk" +
                "OmZhbHNlLG1hcms6J+euoee6v+Wxgid9LHtuYW1lOifkuK3nspfnoILlnqvlsYInLGhlaWdodDonMC4xNScsbG9ja2VkOnRydWUs" +
                "bWFyazon566h57q/5bGCJ30se25hbWU6J+WOn+Wcn+WbnuWhqycsaGVpZ2h0OicxLjAwJyxsb2NrZWQ6ZmFsc2UsbWFyazonJ30s" +
                "e25hbWU6J+egguWeq+WxgicsaGVpZ2h0OicwLjEwJyxsb2NrZWQ6dHJ1ZSxtYXJrOifkupXkuIvlsYInfV07CiAgZnVuY3Rpb24g" +
                "UXVhbnRpdHlEZWZhdWx0c1BhZ2Uob3B0aW9ucyl7b3B0aW9ucz1vcHRpb25zfHx7fTt0aGlzLnJvb3Q9ZG9jdW1lbnQuZ2V0RWxl" +
                "bWVudEJ5SWQob3B0aW9ucy5yb290SWR8fCdkZWZhdWx0UHJvZmlsZXNQYWdlJyk7dGhpcy50YWJzPWRvY3VtZW50LmdldEVsZW1l" +
                "bnRCeUlkKG9wdGlvbnMudGFic0lkfHwnZGVmYXVsdFByb2ZpbGVUYWJzJyk7dGhpcy5lZGl0b3I9ZG9jdW1lbnQuZ2V0RWxlbWVu" +
                "dEJ5SWQob3B0aW9ucy5lZGl0b3JJZHx8J2RlZmF1bHRQcm9maWxlRWRpdG9yJyk7dGhpcy50b2FzdD1vcHRpb25zLnRvYXN0fHxm" +
                "dW5jdGlvbigpe307dGhpcy5wcm9maWxlcz1jbG9uZVByb2ZpbGVzKChvcHRpb25zLmRhdGEmJm9wdGlvbnMuZGF0YS5wcm9maWxl" +
                "cyl8fFtdKTt0aGlzLmJ1aWx0SW5Qcm9maWxlcz1jbG9uZVByb2ZpbGVzKChvcHRpb25zLmJ1aWx0SW5EYXRhJiZvcHRpb25zLmJ1" +
                "aWx0SW5EYXRhLnByb2ZpbGVzKXx8W10pO3RoaXMuYWN0aXZlSW5kZXg9MDt0aGlzLnNlbGVjdGVkTGF5ZXI9e307aWYoIXRoaXMu" +
                "cm9vdHx8IXRoaXMudGFic3x8IXRoaXMuZWRpdG9yKXRocm93IG5ldyBFcnJvcignUXVhbnRpdHlEZWZhdWx0c1BhZ2Ug57y65bCR" +
                "5b+F6KaBIERPTSDlrrnlmajjgIInKTt9CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLnByb2ZpbGVLZXk9ZnVuY3Rp" +
                "b24ocCxpZHgpe3JldHVybiAocC5pZHx8cC5raW5kfHwncHJvZmlsZScpKydfJytpZHg7fTsKICBRdWFudGl0eURlZmF1bHRzUGFn" +
                "ZS5wcm90b3R5cGUucmVuZGVyPWZ1bmN0aW9uKCl7dGhpcy5yZW5kZXJUYWJzKCk7dmFyIHA9dGhpcy5wcm9maWxlc1t0aGlzLmFj" +
                "dGl2ZUluZGV4XXx8dGhpcy5wcm9maWxlc1swXTtpZighcCl7dGhpcy5lZGl0b3IuaW5uZXJIVE1MPSc8ZGl2IGNsYXNzPSJlbXB0" +
                "eS1sYXllciI+5pqC5peg6buY6K6k6KGo5pWw5o2u44CCPC9kaXY+JztyZXR1cm47fXRoaXMuYWN0aXZlSW5kZXg9dGhpcy5wcm9m" +
                "aWxlcy5pbmRleE9mKHApO3ZhciBodG1sPSc8ZGl2IGNsYXNzPSJxZC1ub3RlIj48c3Bhbj7lvZPliY3nsbvlnovvvJo8c3Ryb25n" +
                "PicrZXNjKHAua2luZCkrJzwvc3Ryb25nPjwvc3Bhbj48c3Bhbj7lrZfmrrXmlbDvvJonKyhwLmZpZWxkc3x8W10pLmxlbmd0aCsn" +
                "PC9zcGFuPjwvZGl2PjxkaXYgY2xhc3M9InFkLWZpZWxkcyI+Jztmb3IodmFyIGk9MDtpPChwLmZpZWxkc3x8W10pLmxlbmd0aDtp" +
                "Kyspe3ZhciBmPXAuZmllbGRzW2ldO2h0bWwrPSc8ZGl2IGNsYXNzPSJxZC1maWVsZCAnKyhmLndpZGU/J3dpZGUnOicnKSsnIj48" +
                "bGFiZWw+Jytlc2MoZi5sYWJlbCkrJzwvbGFiZWw+Jyt0aGlzLmNvbnRyb2xIdG1sKHAsZixpKSsoZi5oZWxwPyc8cD4nK2VzYyhm" +
                "LmhlbHApKyc8L3A+JzonJykrJzwvZGl2Pic7fWh0bWwrPSc8L2Rpdj4nO3RoaXMuZWRpdG9yLmlubmVySFRNTD1odG1sO3RoaXMu" +
                "YmluZElucHV0cyhwKTt0aGlzLmJpbmRTdHJ1Y3R1cmVFZGl0b3JzKHApO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90" +
                "eXBlLnJlbmRlclRhYnM9ZnVuY3Rpb24oKXt2YXIgc2VsZj10aGlzO3RoaXMudGFicy5pbm5lckhUTUw9Jyc7dGhpcy5wcm9maWxl" +
                "cy5mb3JFYWNoKGZ1bmN0aW9uKHAsaSl7dmFyIGJ0bj1kb2N1bWVudC5jcmVhdGVFbGVtZW50KCdidXR0b24nKTtidG4uY2xhc3NO" +
                "YW1lPSdxZC10YWIgcHJvZmlsZS10YWIgJysoaT09PXNlbGYuYWN0aXZlSW5kZXg/J2FjdGl2ZSc6JycpO2J0bi50ZXh0Q29udGVu" +
                "dD1wLnRpdGxlfHxwLmtpbmR8fCgn6buY6K6k6KGoICcrKGkrMSkpO2J0bi5hZGRFdmVudExpc3RlbmVyKCdjbGljaycsZnVuY3Rp" +
                "b24oKXtzZWxmLmFjdGl2ZUluZGV4PWk7c2VsZi5yZW5kZXIoKTt9KTtzZWxmLnRhYnMuYXBwZW5kQ2hpbGQoYnRuKTt9KTt9Owog" +
                "IFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5jb250cm9sSHRtbD1mdW5jdGlvbihwLGYsaWR4KXt2YXIgdmFsdWU9Zi52" +
                "YWx1ZXx8Jyc7aWYodGhpcy5pc1N0cnVjdHVyZUZpZWxkKGYpKXJldHVybiB0aGlzLnN0cnVjdHVyZUVkaXRvckh0bWwocCxmLGlk" +
                "eCk7aWYoZi50eXBlPT09J2Jvb2wnKXJldHVybiAnPGxhYmVsIGNsYXNzPSJjaGVjay1maWVsZCI+PGlucHV0IHR5cGU9ImNoZWNr" +
                "Ym94IiBkYXRhLXByb2ZpbGUtaW5wdXQ9IicraWR4KyciICcrKHZhbHVlPT09JzEnPydjaGVja2VkJzonJykrJz48c3Bhbj7lkK/n" +
                "lKg8L3NwYW4+PC9sYWJlbD4nO2lmKGYudHlwZT09PSd0ZXh0YXJlYScpcmV0dXJuICc8dGV4dGFyZWEgZGF0YS1wcm9maWxlLWlu" +
                "cHV0PSInK2lkeCsnIj4nK2VzYyh2YWx1ZSkrJzwvdGV4dGFyZWE+JztpZihmLnR5cGU9PT0nc2VsZWN0Jyl7dmFyIHM9JzxzZWxl" +
                "Y3QgZGF0YS1wcm9maWxlLWlucHV0PSInK2lkeCsnIj4nOyhmLm9wdGlvbnN8fFtdKS5mb3JFYWNoKGZ1bmN0aW9uKG8pe3MrPSc8" +
                "b3B0aW9uIHZhbHVlPSInK2VzYyhvKSsnIiAnKyhvPT09dmFsdWU/J3NlbGVjdGVkJzonJykrJz4nK2VzYyhvKSsnPC9vcHRpb24+" +
                "Jzt9KTtpZigoZi5vcHRpb25zfHxbXSkuaW5kZXhPZih2YWx1ZSk8MCYmdmFsdWUpcys9JzxvcHRpb24gdmFsdWU9IicrZXNjKHZh" +
                "bHVlKSsnIiBzZWxlY3RlZD4nK2VzYyh2YWx1ZSkrJzwvb3B0aW9uPic7cmV0dXJuIHMrJzwvc2VsZWN0Pic7fWlmKGYudHlwZT09" +
                "PSdudW1iZXInKXJldHVybiAnPGlucHV0IHR5cGU9Im51bWJlciIgc3RlcD0iMC4wMSIgZGF0YS1wcm9maWxlLWlucHV0PSInK2lk" +
                "eCsnIiB2YWx1ZT0iJytlc2ModmFsdWUpKyciPic7cmV0dXJuICc8aW5wdXQgdHlwZT0idGV4dCIgZGF0YS1wcm9maWxlLWlucHV0" +
                "PSInK2lkeCsnIiB2YWx1ZT0iJytlc2ModmFsdWUpKyciPic7fTsKICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUuYmlu" +
                "ZElucHV0cz1mdW5jdGlvbihwKXtbXS5zbGljZS5jYWxsKHRoaXMuZWRpdG9yLnF1ZXJ5U2VsZWN0b3JBbGwoJ1tkYXRhLXByb2Zp" +
                "bGUtaW5wdXRdJykpLmZvckVhY2goZnVuY3Rpb24oaW5wdXQpe3ZhciBpZHg9cGFyc2VJbnQoaW5wdXQuZ2V0QXR0cmlidXRlKCdk" +
                "YXRhLXByb2ZpbGUtaW5wdXQnKSwxMCk7ZnVuY3Rpb24gc3luYygpe2lmKCFwLmZpZWxkc1tpZHhdKXJldHVybjtwLmZpZWxkc1tp" +
                "ZHhdLnZhbHVlPWlucHV0LnR5cGU9PT0nY2hlY2tib3gnPyhpbnB1dC5jaGVja2VkPycxJzonMCcpOmlucHV0LnZhbHVlO31pbnB1" +
                "dC5hZGRFdmVudExpc3RlbmVyKCdpbnB1dCcsc3luYyk7aW5wdXQuYWRkRXZlbnRMaXN0ZW5lcignY2hhbmdlJyxzeW5jKTt9KTt9" +
                "OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5pc1N0cnVjdHVyZUZpZWxkPWZ1bmN0aW9uKGYpe3JldHVybiBTdHJp" +
                "bmcoZiYmZi5rZXl8fCcnKS50b0xvd2VyQ2FzZSgpPT09J2JhY2tmaWxsc3RydWN0dXJlJzt9OwogIFF1YW50aXR5RGVmYXVsdHNQ" +
                "YWdlLnByb3RvdHlwZS5lbnN1cmVMYXllcnM9ZnVuY3Rpb24oZil7aWYoIWYubGF5ZXJzKWYubGF5ZXJzPXRoaXMucGFyc2VTdHJ1" +
                "Y3R1cmUoZi52YWx1ZXx8JycpO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLnN0cnVjdHVyZUVkaXRvckh0bWw9" +
                "ZnVuY3Rpb24ocCxmLGlkeCl7dGhpcy5lbnN1cmVMYXllcnMoZik7dmFyIGtleT10aGlzLnByb2ZpbGVLZXkocCxpZHgpO3ZhciBz" +
                "ZWxlY3RlZD10aGlzLnNlbGVjdGVkTGF5ZXJba2V5XTtpZihzZWxlY3RlZD09bnVsbHx8c2VsZWN0ZWQ8MHx8c2VsZWN0ZWQ+PWYu" +
                "bGF5ZXJzLmxlbmd0aCl0aGlzLnNlbGVjdGVkTGF5ZXJba2V5XT1mLmxheWVycy5sZW5ndGg/MDotMTt2YXIgaHRtbD0nPGRpdiBj" +
                "bGFzcz0ic3RydWN0dXJlLWVkaXRvciIgZGF0YS1zdHJ1Y3R1cmUtaW5kZXg9IicraWR4KyciPjxkaXYgY2xhc3M9InN0cnVjdHVy" +
                "ZS1oZWFkIj48ZGl2PjxzdHJvbmc+57uT5p6E5YyW5bGC5YiX6KGoPC9zdHJvbmc+PHNwYW4+5q+P5bGC5aGr5YaZ5bGC5ZCN44CB" +
                "5Y6a5bqm44CB6ZSB5a6a5ZKM566h57q/5bGCL+S6leS4i+Wxguagh+iusO+8m+S/neWtmOaXtuiHquWKqOaLvOWbnuaXp+eJiOWk" +
                "muihjOe7k+aehOWxguaWh+acrOOAgjwvc3Bhbj48L2Rpdj48L2Rpdj48ZGl2IGNsYXNzPSJzdHJ1Y3R1cmUtdG9vbGJhciI+PGRp" +
                "diBjbGFzcz0idG9vbC1sZWZ0Ij48YnV0dG9uIHR5cGU9ImJ1dHRvbiIgY2xhc3M9InNtYWxsLWJ0biIgZGF0YS1sYXllci1hY3Rp" +
                "b249ImFkZCIgZGF0YS1pZHg9IicraWR4KyciPua3u+WKoOWxgjwvYnV0dG9uPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0i" +
                "c21hbGwtYnRuIGRhbmdlciIgZGF0YS1sYXllci1hY3Rpb249ImRlbGV0ZSIgZGF0YS1pZHg9IicraWR4KyciPuWIoOmZpOWxgjwv" +
                "YnV0dG9uPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0ic21hbGwtYnRuIiBkYXRhLWxheWVyLWFjdGlvbj0idXAiIGRhdGEt" +
                "aWR4PSInK2lkeCsnIj7kuIrnp7s8L2J1dHRvbj48YnV0dG9uIHR5cGU9ImJ1dHRvbiIgY2xhc3M9InNtYWxsLWJ0biIgZGF0YS1s" +
                "YXllci1hY3Rpb249ImRvd24iIGRhdGEtaWR4PSInK2lkeCsnIj7kuIvnp7s8L2J1dHRvbj48YnV0dG9uIHR5cGU9ImJ1dHRvbiIg" +
                "Y2xhc3M9InNtYWxsLWJ0biB3YXJuaW5nIiBkYXRhLWxheWVyLWFjdGlvbj0icmVzdG9yZSIgZGF0YS1pZHg9IicraWR4KyciPuaB" +
                "ouWkjeatpOihqOm7mOiupOe7k+aehOWxgjwvYnV0dG9uPjwvZGl2PjwvZGl2PjxkaXYgY2xhc3M9ImxheWVyLXdyYXAiPic7aWYo" +
                "IWYubGF5ZXJzLmxlbmd0aCl7aHRtbCs9JzxkaXYgY2xhc3M9ImVtcHR5LWxheWVyIj7mmoLml6Dnu5PmnoTlsYLvvIzngrnlh7vi" +
                "gJzmt7vliqDlsYLigJ3jgII8L2Rpdj4nO31lbHNle2h0bWwrPSc8dGFibGUgY2xhc3M9ImxheWVyLXRhYmxlIj48dGhlYWQ+PHRy" +
                "Pjx0aCBjbGFzcz0icHJpb3JpdHkiPuS8mOWFiOe6pzwvdGg+PHRoIGNsYXNzPSJuYW1lLWNvbCI+5bGC5ZCNPC90aD48dGggY2xh" +
                "c3M9ImhlaWdodC1jb2wiPuWOmuW6piBtPC90aD48dGggY2xhc3M9ImZsYWctY29sIj7plIHlrpo8L3RoPjx0aCBjbGFzcz0ibWFy" +
                "ay1jb2wiPuagh+iusDwvdGg+PHRoIGNsYXNzPSJyb3ctdG9vbHMiPuihjOaTjeS9nDwvdGg+PC90cj48L3RoZWFkPjx0Ym9keT4n" +
                "O2Zvcih2YXIgcm93PTA7cm93PGYubGF5ZXJzLmxlbmd0aDtyb3crKyl7dmFyIGxheWVyPWYubGF5ZXJzW3Jvd107aHRtbCs9Jzx0" +
                "ciBjbGFzcz0iJysocm93PT09dGhpcy5zZWxlY3RlZExheWVyW2tleV0/J3NlbGVjdGVkJzonJykrJyIgZGF0YS1sYXllci1yb3c9" +
                "Iicrcm93KyciIGRhdGEtaWR4PSInK2lkeCsnIj48dGQgY2xhc3M9InByaW9yaXR5Ij4nKyhyb3crMSkrJzwvdGQ+PHRkIGNsYXNz" +
                "PSJuYW1lLWNvbCI+PGlucHV0IHR5cGU9InRleHQiIGRhdGEtbGF5ZXItZmllbGQ9Im5hbWUiIGRhdGEtaWR4PSInK2lkeCsnIiBk" +
                "YXRhLXJvdz0iJytyb3crJyIgdmFsdWU9IicrZXNjKGxheWVyLm5hbWUpKyciIHBsYWNlaG9sZGVyPSLlpoLvvJrkuK3nspfnoILl" +
                "m57loasiPjwvdGQ+PHRkIGNsYXNzPSJoZWlnaHQtY29sIj48aW5wdXQgdHlwZT0ibnVtYmVyIiBzdGVwPSIwLjAxIiBtaW49IjAi" +
                "IGRhdGEtbGF5ZXItZmllbGQ9ImhlaWdodCIgZGF0YS1pZHg9IicraWR4KyciIGRhdGEtcm93PSInK3JvdysnIiB2YWx1ZT0iJytl" +
                "c2MobGF5ZXIuaGVpZ2h0KSsnIiBwbGFjZWhvbGRlcj0iMC4wMCI+PC90ZD48dGQgY2xhc3M9ImZsYWctY29sIj48aW5wdXQgdHlw" +
                "ZT0iY2hlY2tib3giIGRhdGEtbGF5ZXItZmllbGQ9ImxvY2tlZCIgZGF0YS1pZHg9IicraWR4KyciIGRhdGEtcm93PSInK3Jvdysn" +
                "IiAnKyhsYXllci5sb2NrZWQ/J2NoZWNrZWQnOicnKSsnPjwvdGQ+PHRkIGNsYXNzPSJtYXJrLWNvbCI+PHNlbGVjdCBkYXRhLWxh" +
                "eWVyLWZpZWxkPSJtYXJrIiBkYXRhLWlkeD0iJytpZHgrJyIgZGF0YS1yb3c9Iicrcm93KyciPjxvcHRpb24gdmFsdWU9IiIgJyso" +
                "IWxheWVyLm1hcms/J3NlbGVjdGVkJzonJykrJz7ml6DmoIforrA8L29wdGlvbj48b3B0aW9uIHZhbHVlPSLnrqHnur/lsYIiICcr" +
                "KGxheWVyLm1hcms9PT0n566h57q/5bGCJz8nc2VsZWN0ZWQnOicnKSsnPueuoee6v+Wxgjwvb3B0aW9uPjxvcHRpb24gdmFsdWU9" +
                "IuS6leS4i+WxgiIgJysobGF5ZXIubWFyaz09PSfkupXkuIvlsYInPydzZWxlY3RlZCc6JycpKyc+5LqV5LiL5bGCPC9vcHRpb24+" +
                "PC9zZWxlY3Q+PC90ZD48dGQgY2xhc3M9InJvdy10b29scyI+PGRpdiBjbGFzcz0iaW5saW5lLXRvb2xzIj48YnV0dG9uIHR5cGU9" +
                "ImJ1dHRvbiIgY2xhc3M9Imljb24tYnRuIiBkYXRhLWxheWVyLXJvdy1hY3Rpb249InVwIiBkYXRhLWlkeD0iJytpZHgrJyIgZGF0" +
                "YS1yb3c9Iicrcm93KyciPuKGkTwvYnV0dG9uPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0iaWNvbi1idG4iIGRhdGEtbGF5" +
                "ZXItcm93LWFjdGlvbj0iZG93biIgZGF0YS1pZHg9IicraWR4KyciIGRhdGEtcm93PSInK3JvdysnIj7ihpM8L2J1dHRvbj48YnV0" +
                "dG9uIHR5cGU9ImJ1dHRvbiIgY2xhc3M9Imljb24tYnRuIGRhbmdlciIgZGF0YS1sYXllci1yb3ctYWN0aW9uPSJkZWxldGUiIGRh" +
                "dGEtaWR4PSInK2lkeCsnIiBkYXRhLXJvdz0iJytyb3crJyI+w5c8L2J1dHRvbj48L2Rpdj48L3RkPjwvdHI+Jzt9aHRtbCs9Jzwv" +
                "dGJvZHk+PC90YWJsZT4nO31odG1sKz0nPC9kaXY+PC9kaXY+JztyZXR1cm4gaHRtbDt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdl" +
                "LnByb3RvdHlwZS5jb21tb25MYXllck9wdGlvbnM9ZnVuY3Rpb24ocCl7dmFyIGlzTm9kZT1TdHJpbmcocC5raW5kfHwnJykuaW5k" +
                "ZXhPZign5LqVJyk+PTA7cmV0dXJuIGNvbW1vbkxheWVycy5tYXAoZnVuY3Rpb24obGF5ZXIsaSl7dmFyIGl0ZW09e25hbWU6bGF5" +
                "ZXIubmFtZSxoZWlnaHQ6bGF5ZXIuaGVpZ2h0LGxvY2tlZDpsYXllci5sb2NrZWQsbWFyazpsYXllci5tYXJrfTtpZihpc05vZGUm" +
                "Jml0ZW0ubWFyaz09PSfnrqHnur/lsYInJiZpdGVtLm5hbWUuaW5kZXhPZign5Z6r5bGCJyk+PTApaXRlbS5tYXJrPSfkupXkuIvl" +
                "sYInO3JldHVybiAnPG9wdGlvbiB2YWx1ZT0iJytpKyciPicrZXNjKGl0ZW0ubmFtZSsnICcraXRlbS5oZWlnaHQrKGl0ZW0ubG9j" +
                "a2VkPycg6ZSB5a6aJzonJykrKGl0ZW0ubWFyaz8nICcraXRlbS5tYXJrOicnKSkrJzwvb3B0aW9uPic7fSkuam9pbignJyk7fTsK" +
                "ICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUuYmluZFN0cnVjdHVyZUVkaXRvcnM9ZnVuY3Rpb24ocCl7dmFyIHNlbGY9" +
                "dGhpcztbXS5zbGljZS5jYWxsKHRoaXMuZWRpdG9yLnF1ZXJ5U2VsZWN0b3JBbGwoJ1tkYXRhLWxheWVyLXJvd10nKSkuZm9yRWFj" +
                "aChmdW5jdGlvbihyb3dFbCl7cm93RWwuYWRkRXZlbnRMaXN0ZW5lcignY2xpY2snLGZ1bmN0aW9uKCl7dmFyIGlkeD1wYXJzZUlu" +
                "dChyb3dFbC5nZXRBdHRyaWJ1dGUoJ2RhdGEtaWR4JyksMTApO3ZhciByb3c9cGFyc2VJbnQocm93RWwuZ2V0QXR0cmlidXRlKCdk" +
                "YXRhLWxheWVyLXJvdycpLDEwKTtzZWxmLnNlbGVjdGVkTGF5ZXJbc2VsZi5wcm9maWxlS2V5KHAsaWR4KV09cm93O3NlbGYucmVu" +
                "ZGVyKCk7fSk7fSk7W10uc2xpY2UuY2FsbCh0aGlzLmVkaXRvci5xdWVyeVNlbGVjdG9yQWxsKCdbZGF0YS1sYXllci1maWVsZF0n" +
                "KSkuZm9yRWFjaChmdW5jdGlvbihpbnB1dCl7aW5wdXQuYWRkRXZlbnRMaXN0ZW5lcignY2xpY2snLGZ1bmN0aW9uKGV2KXtldi5z" +
                "dG9wUHJvcGFnYXRpb24oKTt9KTtmdW5jdGlvbiBzeW5jKCl7dmFyIGlkeD1wYXJzZUludChpbnB1dC5nZXRBdHRyaWJ1dGUoJ2Rh" +
                "dGEtaWR4JyksMTApO3ZhciByb3c9cGFyc2VJbnQoaW5wdXQuZ2V0QXR0cmlidXRlKCdkYXRhLXJvdycpLDEwKTt2YXIgZmllbGQ9" +
                "aW5wdXQuZ2V0QXR0cmlidXRlKCdkYXRhLWxheWVyLWZpZWxkJyk7dmFyIGY9cC5maWVsZHNbaWR4XTtzZWxmLmVuc3VyZUxheWVy" +
                "cyhmKTtpZighZi5sYXllcnNbcm93XSlyZXR1cm47aWYoZmllbGQ9PT0nbG9ja2VkJylmLmxheWVyc1tyb3ddLmxvY2tlZD1pbnB1" +
                "dC5jaGVja2VkO2Vsc2UgZi5sYXllcnNbcm93XVtmaWVsZF09aW5wdXQudmFsdWU7c2VsZi5zeW5jU3RydWN0dXJlVmFsdWUoZik7" +
                "c2VsZi51cGRhdGVTdHJ1Y3R1cmVQcmV2aWV3KGlucHV0LGYpO31pbnB1dC5hZGRFdmVudExpc3RlbmVyKCdpbnB1dCcsc3luYyk7" +
                "aW5wdXQuYWRkRXZlbnRMaXN0ZW5lcignY2hhbmdlJyxzeW5jKTt9KTtbXS5zbGljZS5jYWxsKHRoaXMuZWRpdG9yLnF1ZXJ5U2Vs" +
                "ZWN0b3JBbGwoJ1tkYXRhLWxheWVyLWFjdGlvbl0nKSkuZm9yRWFjaChmdW5jdGlvbihidG4pe2J0bi5hZGRFdmVudExpc3RlbmVy" +
                "KCdjbGljaycsZnVuY3Rpb24oZXYpe2V2LnN0b3BQcm9wYWdhdGlvbigpO3NlbGYuaGFuZGxlTGF5ZXJBY3Rpb24ocCxwYXJzZUlu" +
                "dChidG4uZ2V0QXR0cmlidXRlKCdkYXRhLWlkeCcpLDEwKSxidG4uZ2V0QXR0cmlidXRlKCdkYXRhLWxheWVyLWFjdGlvbicpKTt9" +
                "KTt9KTtbXS5zbGljZS5jYWxsKHRoaXMuZWRpdG9yLnF1ZXJ5U2VsZWN0b3JBbGwoJ1tkYXRhLWxheWVyLXJvdy1hY3Rpb25dJykp" +
                "LmZvckVhY2goZnVuY3Rpb24oYnRuKXtidG4uYWRkRXZlbnRMaXN0ZW5lcignY2xpY2snLGZ1bmN0aW9uKGV2KXtldi5zdG9wUHJv" +
                "cGFnYXRpb24oKTt2YXIgaWR4PXBhcnNlSW50KGJ0bi5nZXRBdHRyaWJ1dGUoJ2RhdGEtaWR4JyksMTApO3NlbGYuc2VsZWN0ZWRM" +
                "YXllcltzZWxmLnByb2ZpbGVLZXkocCxpZHgpXT1wYXJzZUludChidG4uZ2V0QXR0cmlidXRlKCdkYXRhLXJvdycpLDEwKTtzZWxm" +
                "LmhhbmRsZUxheWVyQWN0aW9uKHAsaWR4LGJ0bi5nZXRBdHRyaWJ1dGUoJ2RhdGEtbGF5ZXItcm93LWFjdGlvbicpKTt9KTt9KTt9" +
                "OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS51cGRhdGVTdHJ1Y3R1cmVQcmV2aWV3PWZ1bmN0aW9uKGlucHV0LGYp" +
                "e307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLmhhbmRsZUxheWVyQWN0aW9uPWZ1bmN0aW9uKHAsaWR4LGFjdGlv" +
                "bil7dmFyIGY9cC5maWVsZHNbaWR4XTt0aGlzLmVuc3VyZUxheWVycyhmKTt2YXIga2V5PXRoaXMucHJvZmlsZUtleShwLGlkeCk7" +
                "dmFyIHNlbGVjdGVkPXRoaXMuc2VsZWN0ZWRMYXllcltrZXldPT1udWxsPyhmLmxheWVycy5sZW5ndGg/MDotMSk6dGhpcy5zZWxl" +
                "Y3RlZExheWVyW2tleV07aWYoYWN0aW9uPT09J2FkZCcpe2YubGF5ZXJzLnB1c2goe25hbWU6J+aWsOe7k+aehOWxgicsaGVpZ2h0" +
                "OicwLjEwJyxsb2NrZWQ6ZmFsc2UsbWFyazonJ30pO3RoaXMuc2VsZWN0ZWRMYXllcltrZXldPWYubGF5ZXJzLmxlbmd0aC0xO31l" +
                "bHNlIGlmKGFjdGlvbj09PSdyZXN0b3JlJyl7dmFyIGJ1aWx0SW5Qcm9maWxlPXRoaXMuZmluZEJ1aWx0SW5Qcm9maWxlKHApO3Zh" +
                "ciBidWlsdEluRmllbGQ9dGhpcy5maW5kRmllbGQoYnVpbHRJblByb2ZpbGUsJ0JhY2tmaWxsU3RydWN0dXJlJyk7Zi5sYXllcnM9" +
                "dGhpcy5wYXJzZVN0cnVjdHVyZShidWlsdEluRmllbGQ/YnVpbHRJbkZpZWxkLnZhbHVlOicnKTt0aGlzLnNlbGVjdGVkTGF5ZXJb" +
                "a2V5XT1mLmxheWVycy5sZW5ndGg/MDotMTt0aGlzLnRvYXN0KCflt7LmgaLlpI3lvZPliY3ooajnmoTlhoXnva7nu5PmnoTlsYLv" +
                "vIzngrnlh7vkv53lrZjlkI7nlJ/mlYgnLCdpbmZvJyk7fWVsc2UgaWYoc2VsZWN0ZWQ+PTAmJnNlbGVjdGVkPGYubGF5ZXJzLmxl" +
                "bmd0aCl7aWYoYWN0aW9uPT09J2RlbGV0ZScpe2YubGF5ZXJzLnNwbGljZShzZWxlY3RlZCwxKTt0aGlzLnNlbGVjdGVkTGF5ZXJb" +
                "a2V5XT1NYXRoLm1pbihzZWxlY3RlZCxmLmxheWVycy5sZW5ndGgtMSk7fWVsc2UgaWYoYWN0aW9uPT09J3VwJyYmc2VsZWN0ZWQ+" +
                "MCl7dmFyIGE9Zi5sYXllcnMuc3BsaWNlKHNlbGVjdGVkLDEpWzBdO2YubGF5ZXJzLnNwbGljZShzZWxlY3RlZC0xLDAsYSk7dGhp" +
                "cy5zZWxlY3RlZExheWVyW2tleV09c2VsZWN0ZWQtMTt9ZWxzZSBpZihhY3Rpb249PT0nZG93bicmJnNlbGVjdGVkPGYubGF5ZXJz" +
                "Lmxlbmd0aC0xKXt2YXIgYj1mLmxheWVycy5zcGxpY2Uoc2VsZWN0ZWQsMSlbMF07Zi5sYXllcnMuc3BsaWNlKHNlbGVjdGVkKzEs" +
                "MCxiKTt0aGlzLnNlbGVjdGVkTGF5ZXJba2V5XT1zZWxlY3RlZCsxO319dGhpcy5zeW5jU3RydWN0dXJlVmFsdWUoZik7dGhpcy5y" +
                "ZW5kZXIoKTt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5wYXJzZVN0cnVjdHVyZT1mdW5jdGlvbih0ZXh0KXt2" +
                "YXIgbGluZXM9U3RyaW5nKHRleHR8fCcnKS5yZXBsYWNlKC/vvJsvZywnXG4nKS5yZXBsYWNlKC87L2csJ1xuJykucmVwbGFjZSgv" +
                "77yML2csJ1xuJykucmVwbGFjZSgvLC9nLCdcbicpLnJlcGxhY2UoL1xyXG4vZywnXG4nKS5yZXBsYWNlKC9cci9nLCdcbicpLnNw" +
                "bGl0KCdcbicpO3ZhciByZXN1bHQ9W107bGluZXMuZm9yRWFjaChmdW5jdGlvbihyYXcpe3ZhciBsaW5lPVN0cmluZyhyYXd8fCcn" +
                "KS50cmltKCk7aWYoIWxpbmUpcmV0dXJuO3ZhciBsb2NrZWQ9L+mUgeWumnzlm7rlrpovaS50ZXN0KGxpbmUpO3ZhciBtYXJrPS/k" +
                "upXkuIvlsYJ85LqV5LiL5pa55Z6r5bGCL2kudGVzdChsaW5lKT8n5LqV5LiL5bGCJzooL+euoee6v+WxgnznrqHpgZPlsYJ8566h" +
                "5bGCL2kudGVzdChsaW5lKT8n566h57q/5bGCJzonJyk7dmFyIG1hdGNoZXM9bGluZS5tYXRjaCgvWy0rXT9cZCsoPzpcLlxkKyk/" +
                "L2cpO3ZhciBoZWlnaHQ9bWF0Y2hlcyYmbWF0Y2hlcy5sZW5ndGg/bWF0Y2hlc1ttYXRjaGVzLmxlbmd0aC0xXTonJzt2YXIgbmFt" +
                "ZT1saW5lO2lmKGhlaWdodCl7dmFyIHBvcz1saW5lLmxhc3RJbmRleE9mKGhlaWdodCk7bmFtZT1saW5lLnN1YnN0cmluZygwLHBv" +
                "cyk7fW5hbWU9bmFtZS5yZXBsYWNlKC/plIHlrpp85Zu65a6afOeuoee6v+WxgnznrqHpgZPlsYJ8566h5bGCfOS6leS4i+Wxgnzk" +
                "upXkuIvmlrnlnqvlsYIvaWcsJycpLnRyaW0oKTtpZighbmFtZSluYW1lPWxpbmUucmVwbGFjZSgv6ZSB5a6afOWbuuWumnznrqHn" +
                "ur/lsYJ8566h6YGT5bGCfOeuoeWxgnzkupXkuIvlsYJ85LqV5LiL5pa55Z6r5bGCL2lnLCcnKS5yZXBsYWNlKGhlaWdodCwnJyku" +
                "dHJpbSgpO3Jlc3VsdC5wdXNoKHtuYW1lOm5hbWV8fCfnu5PmnoTlsYInLGhlaWdodDpoZWlnaHQsbG9ja2VkOmxvY2tlZCxtYXJr" +
                "Om1hcmt9KTt9KTtyZXR1cm4gcmVzdWx0O307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLmJ1aWxkU3RydWN0dXJl" +
                "VGV4dD1mdW5jdGlvbihsYXllcnMpe3JldHVybiAobGF5ZXJzfHxbXSkubWFwKGZ1bmN0aW9uKGxheWVyKXt2YXIgcGFydHM9W107" +
                "dmFyIG5hbWU9U3RyaW5nKGxheWVyLm5hbWV8fCcnKS50cmltKCk7dmFyIGhlaWdodD1TdHJpbmcobGF5ZXIuaGVpZ2h0fHwnJyku" +
                "dHJpbSgpO2lmKCFuYW1lJiYhaGVpZ2h0KXJldHVybiAnJztwYXJ0cy5wdXNoKG5hbWV8fCfnu5PmnoTlsYInKTtpZihoZWlnaHQp" +
                "cGFydHMucHVzaChoZWlnaHQpO2lmKGxheWVyLmxvY2tlZClwYXJ0cy5wdXNoKCfplIHlrponKTtpZihsYXllci5tYXJrKXBhcnRz" +
                "LnB1c2gobGF5ZXIubWFyayk7cmV0dXJuIHBhcnRzLmpvaW4oJyAnKTt9KS5maWx0ZXIoZnVuY3Rpb24obGluZSl7cmV0dXJuICEh" +
                "bGluZTt9KS5qb2luKCdcbicpO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLnN5bmNTdHJ1Y3R1cmVWYWx1ZT1m" +
                "dW5jdGlvbihmKXt0aGlzLmVuc3VyZUxheWVycyhmKTtmLnZhbHVlPXRoaXMuYnVpbGRTdHJ1Y3R1cmVUZXh0KGYubGF5ZXJzKTt9" +
                "OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5maW5kRmllbGQ9ZnVuY3Rpb24ocHJvZmlsZSxrZXkpe2lmKCFwcm9m" +
                "aWxlKXJldHVybiBudWxsO2tleT1TdHJpbmcoa2V5fHwnJykudG9Mb3dlckNhc2UoKTtmb3IodmFyIGk9MDtpPChwcm9maWxlLmZp" +
                "ZWxkc3x8W10pLmxlbmd0aDtpKyspe2lmKFN0cmluZyhwcm9maWxlLmZpZWxkc1tpXS5rZXl8fCcnKS50b0xvd2VyQ2FzZSgpPT09" +
                "a2V5KXJldHVybiBwcm9maWxlLmZpZWxkc1tpXTt9cmV0dXJuIG51bGw7fTsKICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5" +
                "cGUuZmluZEJ1aWx0SW5Qcm9maWxlPWZ1bmN0aW9uKHByb2ZpbGUpe2Zvcih2YXIgaT0wO2k8dGhpcy5idWlsdEluUHJvZmlsZXMu" +
                "bGVuZ3RoO2krKyl7aWYodGhpcy5idWlsdEluUHJvZmlsZXNbaV0uaWQ9PT1wcm9maWxlLmlkfHx0aGlzLmJ1aWx0SW5Qcm9maWxl" +
                "c1tpXS5raW5kPT09cHJvZmlsZS5raW5kKXJldHVybiB0aGlzLmJ1aWx0SW5Qcm9maWxlc1tpXTt9cmV0dXJuIG51bGw7fTsKICBR" +
                "dWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUuYnVpbGRQYXlsb2FkPWZ1bmN0aW9uKCl7dmFyIHNlbGY9dGhpczt2YXIgbGlu" +
                "ZXM9W107dGhpcy5wcm9maWxlcy5mb3JFYWNoKGZ1bmN0aW9uKHApeyhwLmZpZWxkc3x8W10pLmZvckVhY2goZnVuY3Rpb24oZil7" +
                "aWYoc2VsZi5pc1N0cnVjdHVyZUZpZWxkKGYpKXNlbGYuc3luY1N0cnVjdHVyZVZhbHVlKGYpO2xpbmVzLnB1c2goW3Aua2luZCxm" +
                "LmtleSxmLnR5cGUsZi52YWx1ZXx8JyddLm1hcChlbmNvZGU2NCkuam9pbignXHQnKSk7fSk7fSk7cmV0dXJuIGxpbmVzLmpvaW4o" +
                "J1xuJyk7fTsKICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUucmVzdG9yZUJ1aWx0SW49ZnVuY3Rpb24oKXtpZighY29u" +
                "ZmlybSgn5oGi5aSN5YaF572u5bGe5oCn6buY6K6k6KGo5Lya6KaG55uW5b2T5YmN6aG16Z2i57yW6L6R5YaF5a6577yM5L+d5a2Y" +
                "5YmN5LiN5Lya5YaZ5YWl5paH5Lu244CC5piv5ZCm57un57ut77yfJykpcmV0dXJuO3RoaXMucHJvZmlsZXM9Y2xvbmVQcm9maWxl" +
                "cyh0aGlzLmJ1aWx0SW5Qcm9maWxlcyk7dGhpcy5zZWxlY3RlZExheWVyPXt9O3RoaXMuYWN0aXZlSW5kZXg9MDt0aGlzLnJlbmRl" +
                "cigpO3RoaXMudG9hc3QoJ+W3suaBouWkjeS4uuWGhee9rum7mOiupOihqO+8jOeCueWHu+S/neWtmOWQjueUn+aViCcsJ2luZm8n" +
                "KTt9OwogIHcuQ0RCb3hRdWFudGl0eURlZmF1bHRzUGFnZT17Y3JlYXRlOmZ1bmN0aW9uKG9wdGlvbnMpe3JldHVybiBuZXcgUXVh" +
                "bnRpdHlEZWZhdWx0c1BhZ2Uob3B0aW9ucyk7fSxjbG9uZVByb2ZpbGVzOmNsb25lUHJvZmlsZXMsU3RydWN0dXJlTGF5ZXJFZGl0" +
                "b3I6e3BhcnNlOmZ1bmN0aW9uKHRleHQpe3JldHVybiBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUucGFyc2VTdHJ1Y3R1" +
                "cmUodGV4dCk7fSxidWlsZFRleHQ6ZnVuY3Rpb24obGF5ZXJzKXtyZXR1cm4gUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBl" +
                "LmJ1aWxkU3RydWN0dXJlVGV4dChsYXllcnMpO319fTsKfSkod2luZG93KTsKCnZhciBkZWZhdWx0UHJvZmlsZXNXaWRnZXQ9bnVs" +
                "bDsKZnVuY3Rpb24gZW5zdXJlRGVmYXVsdFByb2ZpbGVzV2lkZ2V0KCl7CiAgaWYoIWRlZmF1bHRQcm9maWxlc1dpZGdldCl7ZGVm" +
                "YXVsdFByb2ZpbGVzV2lkZ2V0PXdpbmRvdy5DREJveFF1YW50aXR5RGVmYXVsdHNQYWdlLmNyZWF0ZSh7cm9vdElkOidkZWZhdWx0" +
                "UHJvZmlsZXNQYWdlJyx0YWJzSWQ6J2RlZmF1bHRQcm9maWxlVGFicycsZWRpdG9ySWQ6J2RlZmF1bHRQcm9maWxlRWRpdG9yJyxk" +
                "YXRhOntwcm9maWxlczpkZWZhdWx0UHJvZmlsZXN9LGJ1aWx0SW5EYXRhOntwcm9maWxlczpidWlsdEluRGVmYXVsdFByb2ZpbGVz" +
                "fSx0b2FzdDp0b2FzdH0pO30KICByZXR1cm4gZGVmYXVsdFByb2ZpbGVzV2lkZ2V0Owp9CmZ1bmN0aW9uIGNsb25lUHJvZmlsZXMo" +
                "c291cmNlKXtyZXR1cm4gd2luZG93LkNEQm94UXVhbnRpdHlEZWZhdWx0c1BhZ2UuY2xvbmVQcm9maWxlcyhzb3VyY2UpO30KZnVu" +
                "Y3Rpb24gcmVuZGVyRGVmYXVsdFByb2ZpbGVzKCl7dmFyIHdpZGdldD1lbnN1cmVEZWZhdWx0UHJvZmlsZXNXaWRnZXQoKTt3aWRn" +
                "ZXQucmVuZGVyKCk7ZGVmYXVsdFByb2ZpbGVzPXdpZGdldC5wcm9maWxlczthY3RpdmVQcm9maWxlSW5kZXg9d2lkZ2V0LmFjdGl2" +
                "ZUluZGV4O30KZnVuY3Rpb24gYnVpbGREZWZhdWx0UHJvZmlsZXNQYXlsb2FkKCl7dmFyIHdpZGdldD1lbnN1cmVEZWZhdWx0UHJv" +
                "ZmlsZXNXaWRnZXQoKTtyZXR1cm4gd2lkZ2V0LmJ1aWxkUGF5bG9hZCgpO30KZnVuY3Rpb24gc2F2ZURlZmF1bHRQcm9maWxlcygp" +
                "e3Bvc3QoJ3NhdmVEZWZhdWx0UHJvZmlsZXMnLGJ1aWxkRGVmYXVsdFByb2ZpbGVzUGF5bG9hZCgpKTt9CmZ1bmN0aW9uIHJlc3Rv" +
                "cmVEZWZhdWx0UHJvZmlsZXMoKXt2YXIgd2lkZ2V0PWVuc3VyZURlZmF1bHRQcm9maWxlc1dpZGdldCgpO3dpZGdldC5yZXN0b3Jl" +
                "QnVpbHRJbigpO2RlZmF1bHRQcm9maWxlcz13aWRnZXQucHJvZmlsZXM7YWN0aXZlUHJvZmlsZUluZGV4PXdpZGdldC5hY3RpdmVJ" +
                "bmRleDt9Cg==";

        private const string StandaloneBootstrapScriptBase64 =
                "CihmdW5jdGlvbigpewogIGZ1bmN0aW9uIHBvc3QobmFtZSxhcmcpe2lmKHdpbmRvdy5jaHJvbWUmJmNocm9tZS53ZWJ2aWV3KXtj" +
                "aHJvbWUud2Vidmlldy5wb3N0TWVzc2FnZSgnc3R1ZGlvfCcrbmFtZSsnfCcrZW5jb2RlVVJJQ29tcG9uZW50KGFyZ3x8JycpKTt9" +
                "fQogIGZ1bmN0aW9uIGh0bWxFc2NhcGUodmFsdWUpe3JldHVybiBTdHJpbmcodmFsdWU9PW51bGw/Jyc6dmFsdWUpLnJlcGxhY2Uo" +
                "L1smPD4iJ10vZyxmdW5jdGlvbihjKXtyZXR1cm4geycmJzonJmFtcDsnLCc8JzonJmx0OycsJz4nOicmZ3Q7JywnIic6JyZxdW90" +
                "OycsIiciOicmIzM5Oyd9W2NdO30pO30KICBmdW5jdGlvbiB0b2FzdChtZXNzYWdlLGtpbmQpe3ZhciBzdGFjaz1kb2N1bWVudC5n" +
                "ZXRFbGVtZW50QnlJZCgndG9hc3RTdGFjaycpO2lmKCFzdGFjaylyZXR1cm47dmFyIG5vZGU9ZG9jdW1lbnQuY3JlYXRlRWxlbWVu" +
                "dCgnZGl2Jyk7bm9kZS5jbGFzc05hbWU9J3RvYXN0ICcrKGtpbmR8fCdpbmZvJyk7bm9kZS50ZXh0Q29udGVudD1tZXNzYWdlfHwn" +
                "JztzdGFjay5hcHBlbmRDaGlsZChub2RlKTtzZXRUaW1lb3V0KGZ1bmN0aW9uKCl7bm9kZS5zdHlsZS5vcGFjaXR5PScwJztub2Rl" +
                "LnN0eWxlLnRyYW5zZm9ybT0ndHJhbnNsYXRlWCgxMHB4KSc7fSwyNjAwKTtzZXRUaW1lb3V0KGZ1bmN0aW9uKCl7aWYobm9kZS5w" +
                "YXJlbnROb2RlKW5vZGUucGFyZW50Tm9kZS5yZW1vdmVDaGlsZChub2RlKTt9LDMxMDApO30gd2luZG93LkNEQm94U3R1ZGlvVG9h" +
                "c3Q9dG9hc3Q7CiAgZnVuY3Rpb24gc2hvd0Vycm9yKHRpdGxlLGRldGFpbCl7dmFyIHJvb3Q9ZG9jdW1lbnQuZ2V0RWxlbWVudEJ5" +
                "SWQoJ2RlZmF1bHRQcm9maWxlc1BhZ2UnKXx8ZG9jdW1lbnQuYm9keTtyb290LmlubmVySFRNTD0nPGRpdiBjbGFzcz0icWQtZXJy" +
                "b3IiPjxkaXYgY2xhc3M9InFkLWVycm9yLWNhcmQiPjxoMj4nK2h0bWxFc2NhcGUodGl0bGV8fCfpobXpnaLliJ3lp4vljJblpLHo" +
                "tKUnKSsnPC9oMj48cD7lsZ7mgKfpu5jorqTooaggV2ViVmlldzIg6aG16Z2i5pyq6IO95a6M5oiQ5Yid5aeL5YyW77yM5penIFdp" +
                "bkZvcm1zIOm7mOiupOihqOS7jeWPr+S9v+eUqOOAgjwvcD48cHJlPicraHRtbEVzY2FwZShkZXRhaWx8fCfmnKrnn6XplJnor68n" +
                "KSsnPC9wcmU+PGJ1dHRvbiBjbGFzcz0icWQtYnRuIGRhbmdlciIgb25jbGljaz0iY2hyb21lJiZjaHJvbWUud2VidmlldyYmY2hy" +
                "b21lLndlYnZpZXcucG9zdE1lc3NhZ2UoXCdzdHVkaW98Y2xvc2V8XCcpIj7lhbPpl608L2J1dHRvbj48L2Rpdj48L2Rpdj4nO30K" +
                "ICB3aW5kb3cuQ0RCb3hTdHVkaW9TaG93RXJyb3I9c2hvd0Vycm9yOwoKKGZ1bmN0aW9uKHcpewogIGZ1bmN0aW9uIGVzYyh2YWx1" +
                "ZSl7cmV0dXJuIFN0cmluZyh2YWx1ZT09bnVsbD8nJzp2YWx1ZSkucmVwbGFjZSgvWyY8PiInXS9nLGZ1bmN0aW9uKGMpe3JldHVy" +
                "biB7JyYnOicmYW1wOycsJzwnOicmbHQ7JywnPic6JyZndDsnLCciJzonJnF1b3Q7JywiJyI6JyYjMzk7J31bY107fSk7fQogIGZ1" +
                "bmN0aW9uIGNsb25lUHJvZmlsZXMoc291cmNlKXtyZXR1cm4gKHNvdXJjZXx8W10pLm1hcChmdW5jdGlvbihwKXtyZXR1cm4ge2lk" +
                "OnAuaWR8fCcnLHRpdGxlOnAudGl0bGV8fCcnLGtpbmQ6cC5raW5kfHwnJyxmaWVsZHM6KHAuZmllbGRzfHxbXSkubWFwKGZ1bmN0" +
                "aW9uKGYpe3JldHVybiB7a2V5OmYua2V5fHwnJyxsYWJlbDpmLmxhYmVsfHwnJyx0eXBlOmYudHlwZXx8J3RleHQnLHZhbHVlOmYu" +
                "dmFsdWV8fCcnLHdpZGU6ISFmLndpZGUsaGVscDpmLmhlbHB8fCcnLG9wdGlvbnM6KGYub3B0aW9uc3x8W10pLnNsaWNlKDApfTt9" +
                "KX07fSk7fQogIGZ1bmN0aW9uIGVuY29kZTY0KHZhbHVlKXtyZXR1cm4gYnRvYSh1bmVzY2FwZShlbmNvZGVVUklDb21wb25lbnQo" +
                "dmFsdWV8fCcnKSkpO30KICB2YXIgY29tbW9uTGF5ZXJzPVt7bmFtZTonQzI156C85oGi5aSNJyxoZWlnaHQ6JzAuMjUnLGxvY2tl" +
                "ZDp0cnVlLG1hcms6Jyd9LHtuYW1lOifnoo7nn7PlnqvlsYInLGhlaWdodDonMC4xMCcsbG9ja2VkOnRydWUsbWFyazonJ30se25h" +
                "bWU6J+S4reeyl+egguWbnuWhqycsaGVpZ2h0OicwLjgwJyxsb2NrZWQ6ZmFsc2UsbWFyazon566h57q/5bGCJ30se25hbWU6J+S4" +
                "reeyl+egguWeq+WxgicsaGVpZ2h0OicwLjE1Jyxsb2NrZWQ6dHJ1ZSxtYXJrOifnrqHnur/lsYInfSx7bmFtZTon5Y6f5Zyf5Zue" +
                "5aGrJyxoZWlnaHQ6JzEuMDAnLGxvY2tlZDpmYWxzZSxtYXJrOicnfSx7bmFtZTon56CC5Z6r5bGCJyxoZWlnaHQ6JzAuMTAnLGxv" +
                "Y2tlZDp0cnVlLG1hcms6J+S6leS4i+Wxgid9XTsKICBmdW5jdGlvbiBRdWFudGl0eURlZmF1bHRzUGFnZShvcHRpb25zKXtvcHRp" +
                "b25zPW9wdGlvbnN8fHt9O3RoaXMucm9vdD1kb2N1bWVudC5nZXRFbGVtZW50QnlJZChvcHRpb25zLnJvb3RJZHx8J2RlZmF1bHRQ" +
                "cm9maWxlc1BhZ2UnKTt0aGlzLnRhYnM9ZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQob3B0aW9ucy50YWJzSWR8fCdkZWZhdWx0UHJv" +
                "ZmlsZVRhYnMnKTt0aGlzLmVkaXRvcj1kb2N1bWVudC5nZXRFbGVtZW50QnlJZChvcHRpb25zLmVkaXRvcklkfHwnZGVmYXVsdFBy" +
                "b2ZpbGVFZGl0b3InKTt0aGlzLnRvYXN0PW9wdGlvbnMudG9hc3R8fGZ1bmN0aW9uKCl7fTt0aGlzLnByb2ZpbGVzPWNsb25lUHJv" +
                "ZmlsZXMoKG9wdGlvbnMuZGF0YSYmb3B0aW9ucy5kYXRhLnByb2ZpbGVzKXx8W10pO3RoaXMuYnVpbHRJblByb2ZpbGVzPWNsb25l" +
                "UHJvZmlsZXMoKG9wdGlvbnMuYnVpbHRJbkRhdGEmJm9wdGlvbnMuYnVpbHRJbkRhdGEucHJvZmlsZXMpfHxbXSk7dGhpcy5hY3Rp" +
                "dmVJbmRleD0wO3RoaXMuc2VsZWN0ZWRMYXllcj17fTtpZighdGhpcy5yb290fHwhdGhpcy50YWJzfHwhdGhpcy5lZGl0b3IpdGhy" +
                "b3cgbmV3IEVycm9yKCdRdWFudGl0eURlZmF1bHRzUGFnZSDnvLrlsJHlv4XopoEgRE9NIOWuueWZqOOAgicpO30KICBRdWFudGl0" +
                "eURlZmF1bHRzUGFnZS5wcm90b3R5cGUucHJvZmlsZUtleT1mdW5jdGlvbihwLGlkeCl7cmV0dXJuIChwLmlkfHxwLmtpbmR8fCdw" +
                "cm9maWxlJykrJ18nK2lkeDt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5yZW5kZXI9ZnVuY3Rpb24oKXt0aGlz" +
                "LnJlbmRlclRhYnMoKTt2YXIgcD10aGlzLnByb2ZpbGVzW3RoaXMuYWN0aXZlSW5kZXhdfHx0aGlzLnByb2ZpbGVzWzBdO2lmKCFw" +
                "KXt0aGlzLmVkaXRvci5pbm5lckhUTUw9JzxkaXYgY2xhc3M9ImVtcHR5LWxheWVyIj7mmoLml6Dpu5jorqTooajmlbDmja7jgII8" +
                "L2Rpdj4nO3JldHVybjt9dGhpcy5hY3RpdmVJbmRleD10aGlzLnByb2ZpbGVzLmluZGV4T2YocCk7dmFyIGh0bWw9JzxkaXYgY2xh" +
                "c3M9InFkLW5vdGUiPjxzcGFuPuW9k+WJjeexu+Wei++8mjxzdHJvbmc+Jytlc2MocC5raW5kKSsnPC9zdHJvbmc+PC9zcGFuPjxz" +
                "cGFuPuWtl+auteaVsO+8micrKHAuZmllbGRzfHxbXSkubGVuZ3RoKyc8L3NwYW4+PC9kaXY+PGRpdiBjbGFzcz0icWQtZmllbGRz" +
                "Ij4nO2Zvcih2YXIgaT0wO2k8KHAuZmllbGRzfHxbXSkubGVuZ3RoO2krKyl7dmFyIGY9cC5maWVsZHNbaV07aHRtbCs9JzxkaXYg" +
                "Y2xhc3M9InFkLWZpZWxkICcrKGYud2lkZT8nd2lkZSc6JycpKyciPjxsYWJlbD4nK2VzYyhmLmxhYmVsKSsnPC9sYWJlbD4nK3Ro" +
                "aXMuY29udHJvbEh0bWwocCxmLGkpKyhmLmhlbHA/JzxwPicrZXNjKGYuaGVscCkrJzwvcD4nOicnKSsnPC9kaXY+Jzt9aHRtbCs9" +
                "JzwvZGl2Pic7dGhpcy5lZGl0b3IuaW5uZXJIVE1MPWh0bWw7dGhpcy5iaW5kSW5wdXRzKHApO3RoaXMuYmluZFN0cnVjdHVyZUVk" +
                "aXRvcnMocCk7fTsKICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUucmVuZGVyVGFicz1mdW5jdGlvbigpe3ZhciBzZWxm" +
                "PXRoaXM7dGhpcy50YWJzLmlubmVySFRNTD0nJzt0aGlzLnByb2ZpbGVzLmZvckVhY2goZnVuY3Rpb24ocCxpKXt2YXIgYnRuPWRv" +
                "Y3VtZW50LmNyZWF0ZUVsZW1lbnQoJ2J1dHRvbicpO2J0bi5jbGFzc05hbWU9J3FkLXRhYiBwcm9maWxlLXRhYiAnKyhpPT09c2Vs" +
                "Zi5hY3RpdmVJbmRleD8nYWN0aXZlJzonJyk7YnRuLnRleHRDb250ZW50PXAudGl0bGV8fHAua2luZHx8KCfpu5jorqTooaggJyso" +
                "aSsxKSk7YnRuLmFkZEV2ZW50TGlzdGVuZXIoJ2NsaWNrJyxmdW5jdGlvbigpe3NlbGYuYWN0aXZlSW5kZXg9aTtzZWxmLnJlbmRl" +
                "cigpO30pO3NlbGYudGFicy5hcHBlbmRDaGlsZChidG4pO30pO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLmNv" +
                "bnRyb2xIdG1sPWZ1bmN0aW9uKHAsZixpZHgpe3ZhciB2YWx1ZT1mLnZhbHVlfHwnJztpZih0aGlzLmlzU3RydWN0dXJlRmllbGQo" +
                "ZikpcmV0dXJuIHRoaXMuc3RydWN0dXJlRWRpdG9ySHRtbChwLGYsaWR4KTtpZihmLnR5cGU9PT0nYm9vbCcpcmV0dXJuICc8bGFi" +
                "ZWwgY2xhc3M9ImNoZWNrLWZpZWxkIj48aW5wdXQgdHlwZT0iY2hlY2tib3giIGRhdGEtcHJvZmlsZS1pbnB1dD0iJytpZHgrJyIg" +
                "JysodmFsdWU9PT0nMSc/J2NoZWNrZWQnOicnKSsnPjxzcGFuPuWQr+eUqDwvc3Bhbj48L2xhYmVsPic7aWYoZi50eXBlPT09J3Rl" +
                "eHRhcmVhJylyZXR1cm4gJzx0ZXh0YXJlYSBkYXRhLXByb2ZpbGUtaW5wdXQ9IicraWR4KyciPicrZXNjKHZhbHVlKSsnPC90ZXh0" +
                "YXJlYT4nO2lmKGYudHlwZT09PSdzZWxlY3QnKXt2YXIgcz0nPHNlbGVjdCBkYXRhLXByb2ZpbGUtaW5wdXQ9IicraWR4KyciPic7" +
                "KGYub3B0aW9uc3x8W10pLmZvckVhY2goZnVuY3Rpb24obyl7cys9JzxvcHRpb24gdmFsdWU9IicrZXNjKG8pKyciICcrKG89PT12" +
                "YWx1ZT8nc2VsZWN0ZWQnOicnKSsnPicrZXNjKG8pKyc8L29wdGlvbj4nO30pO2lmKChmLm9wdGlvbnN8fFtdKS5pbmRleE9mKHZh" +
                "bHVlKTwwJiZ2YWx1ZSlzKz0nPG9wdGlvbiB2YWx1ZT0iJytlc2ModmFsdWUpKyciIHNlbGVjdGVkPicrZXNjKHZhbHVlKSsnPC9v" +
                "cHRpb24+JztyZXR1cm4gcysnPC9zZWxlY3Q+Jzt9aWYoZi50eXBlPT09J251bWJlcicpcmV0dXJuICc8aW5wdXQgdHlwZT0ibnVt" +
                "YmVyIiBzdGVwPSIwLjAxIiBkYXRhLXByb2ZpbGUtaW5wdXQ9IicraWR4KyciIHZhbHVlPSInK2VzYyh2YWx1ZSkrJyI+JztyZXR1" +
                "cm4gJzxpbnB1dCB0eXBlPSJ0ZXh0IiBkYXRhLXByb2ZpbGUtaW5wdXQ9IicraWR4KyciIHZhbHVlPSInK2VzYyh2YWx1ZSkrJyI+" +
                "Jzt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5iaW5kSW5wdXRzPWZ1bmN0aW9uKHApe1tdLnNsaWNlLmNhbGwo" +
                "dGhpcy5lZGl0b3IucXVlcnlTZWxlY3RvckFsbCgnW2RhdGEtcHJvZmlsZS1pbnB1dF0nKSkuZm9yRWFjaChmdW5jdGlvbihpbnB1" +
                "dCl7dmFyIGlkeD1wYXJzZUludChpbnB1dC5nZXRBdHRyaWJ1dGUoJ2RhdGEtcHJvZmlsZS1pbnB1dCcpLDEwKTtmdW5jdGlvbiBz" +
                "eW5jKCl7aWYoIXAuZmllbGRzW2lkeF0pcmV0dXJuO3AuZmllbGRzW2lkeF0udmFsdWU9aW5wdXQudHlwZT09PSdjaGVja2JveCc/" +
                "KGlucHV0LmNoZWNrZWQ/JzEnOicwJyk6aW5wdXQudmFsdWU7fWlucHV0LmFkZEV2ZW50TGlzdGVuZXIoJ2lucHV0JyxzeW5jKTtp" +
                "bnB1dC5hZGRFdmVudExpc3RlbmVyKCdjaGFuZ2UnLHN5bmMpO30pO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBl" +
                "LmlzU3RydWN0dXJlRmllbGQ9ZnVuY3Rpb24oZil7cmV0dXJuIFN0cmluZyhmJiZmLmtleXx8JycpLnRvTG93ZXJDYXNlKCk9PT0n" +
                "YmFja2ZpbGxzdHJ1Y3R1cmUnO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLmVuc3VyZUxheWVycz1mdW5jdGlv" +
                "bihmKXtpZighZi5sYXllcnMpZi5sYXllcnM9dGhpcy5wYXJzZVN0cnVjdHVyZShmLnZhbHVlfHwnJyk7fTsKICBRdWFudGl0eURl" +
                "ZmF1bHRzUGFnZS5wcm90b3R5cGUuc3RydWN0dXJlRWRpdG9ySHRtbD1mdW5jdGlvbihwLGYsaWR4KXt0aGlzLmVuc3VyZUxheWVy" +
                "cyhmKTt2YXIga2V5PXRoaXMucHJvZmlsZUtleShwLGlkeCk7dmFyIHNlbGVjdGVkPXRoaXMuc2VsZWN0ZWRMYXllcltrZXldO2lm" +
                "KHNlbGVjdGVkPT1udWxsfHxzZWxlY3RlZDwwfHxzZWxlY3RlZD49Zi5sYXllcnMubGVuZ3RoKXRoaXMuc2VsZWN0ZWRMYXllcltr" +
                "ZXldPWYubGF5ZXJzLmxlbmd0aD8wOi0xO3ZhciBodG1sPSc8ZGl2IGNsYXNzPSJzdHJ1Y3R1cmUtZWRpdG9yIiBkYXRhLXN0cnVj" +
                "dHVyZS1pbmRleD0iJytpZHgrJyI+PGRpdiBjbGFzcz0ic3RydWN0dXJlLWhlYWQiPjxkaXY+PHN0cm9uZz7nu5PmnoTljJblsYLl" +
                "iJfooag8L3N0cm9uZz48c3Bhbj7mr4/lsYLloavlhpnlsYLlkI3jgIHljprluqbjgIHplIHlrprlkoznrqHnur/lsYIv5LqV5LiL" +
                "5bGC5qCH6K6w77yb5L+d5a2Y5pe26Ieq5Yqo5ou85Zue5pen54mI5aSa6KGM57uT5p6E5bGC5paH5pys44CCPC9zcGFuPjwvZGl2" +
                "PjwvZGl2PjxkaXYgY2xhc3M9InN0cnVjdHVyZS10b29sYmFyIj48ZGl2IGNsYXNzPSJ0b29sLWxlZnQiPjxidXR0b24gdHlwZT0i" +
                "YnV0dG9uIiBjbGFzcz0ic21hbGwtYnRuIiBkYXRhLWxheWVyLWFjdGlvbj0iYWRkIiBkYXRhLWlkeD0iJytpZHgrJyI+5re75Yqg" +
                "5bGCPC9idXR0b24+PGJ1dHRvbiB0eXBlPSJidXR0b24iIGNsYXNzPSJzbWFsbC1idG4gZGFuZ2VyIiBkYXRhLWxheWVyLWFjdGlv" +
                "bj0iZGVsZXRlIiBkYXRhLWlkeD0iJytpZHgrJyI+5Yig6Zmk5bGCPC9idXR0b24+PGJ1dHRvbiB0eXBlPSJidXR0b24iIGNsYXNz" +
                "PSJzbWFsbC1idG4iIGRhdGEtbGF5ZXItYWN0aW9uPSJ1cCIgZGF0YS1pZHg9IicraWR4KyciPuS4iuenuzwvYnV0dG9uPjxidXR0" +
                "b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0ic21hbGwtYnRuIiBkYXRhLWxheWVyLWFjdGlvbj0iZG93biIgZGF0YS1pZHg9IicraWR4" +
                "KyciPuS4i+enuzwvYnV0dG9uPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0ic21hbGwtYnRuIHdhcm5pbmciIGRhdGEtbGF5" +
                "ZXItYWN0aW9uPSJyZXN0b3JlIiBkYXRhLWlkeD0iJytpZHgrJyI+5oGi5aSN5q2k6KGo6buY6K6k57uT5p6E5bGCPC9idXR0b24+" +
                "PC9kaXY+PC9kaXY+PGRpdiBjbGFzcz0ibGF5ZXItd3JhcCI+JztpZighZi5sYXllcnMubGVuZ3RoKXtodG1sKz0nPGRpdiBjbGFz" +
                "cz0iZW1wdHktbGF5ZXIiPuaaguaXoOe7k+aehOWxgu+8jOeCueWHu+KAnOa3u+WKoOWxguKAneOAgjwvZGl2Pic7fWVsc2V7aHRt" +
                "bCs9Jzx0YWJsZSBjbGFzcz0ibGF5ZXItdGFibGUiPjx0aGVhZD48dHI+PHRoIGNsYXNzPSJwcmlvcml0eSI+5LyY5YWI57qnPC90" +
                "aD48dGggY2xhc3M9Im5hbWUtY29sIj7lsYLlkI08L3RoPjx0aCBjbGFzcz0iaGVpZ2h0LWNvbCI+5Y6a5bqmIG08L3RoPjx0aCBj" +
                "bGFzcz0iZmxhZy1jb2wiPumUgeWumjwvdGg+PHRoIGNsYXNzPSJtYXJrLWNvbCI+5qCH6K6wPC90aD48dGggY2xhc3M9InJvdy10" +
                "b29scyI+6KGM5pON5L2cPC90aD48L3RyPjwvdGhlYWQ+PHRib2R5Pic7Zm9yKHZhciByb3c9MDtyb3c8Zi5sYXllcnMubGVuZ3Ro" +
                "O3JvdysrKXt2YXIgbGF5ZXI9Zi5sYXllcnNbcm93XTtodG1sKz0nPHRyIGNsYXNzPSInKyhyb3c9PT10aGlzLnNlbGVjdGVkTGF5" +
                "ZXJba2V5XT8nc2VsZWN0ZWQnOicnKSsnIiBkYXRhLWxheWVyLXJvdz0iJytyb3crJyIgZGF0YS1pZHg9IicraWR4KyciPjx0ZCBj" +
                "bGFzcz0icHJpb3JpdHkiPicrKHJvdysxKSsnPC90ZD48dGQgY2xhc3M9Im5hbWUtY29sIj48aW5wdXQgdHlwZT0idGV4dCIgZGF0" +
                "YS1sYXllci1maWVsZD0ibmFtZSIgZGF0YS1pZHg9IicraWR4KyciIGRhdGEtcm93PSInK3JvdysnIiB2YWx1ZT0iJytlc2MobGF5" +
                "ZXIubmFtZSkrJyIgcGxhY2Vob2xkZXI9IuWmgu+8muS4reeyl+egguWbnuWhqyI+PC90ZD48dGQgY2xhc3M9ImhlaWdodC1jb2wi" +
                "PjxpbnB1dCB0eXBlPSJudW1iZXIiIHN0ZXA9IjAuMDEiIG1pbj0iMCIgZGF0YS1sYXllci1maWVsZD0iaGVpZ2h0IiBkYXRhLWlk" +
                "eD0iJytpZHgrJyIgZGF0YS1yb3c9Iicrcm93KyciIHZhbHVlPSInK2VzYyhsYXllci5oZWlnaHQpKyciIHBsYWNlaG9sZGVyPSIw" +
                "LjAwIj48L3RkPjx0ZCBjbGFzcz0iZmxhZy1jb2wiPjxpbnB1dCB0eXBlPSJjaGVja2JveCIgZGF0YS1sYXllci1maWVsZD0ibG9j" +
                "a2VkIiBkYXRhLWlkeD0iJytpZHgrJyIgZGF0YS1yb3c9Iicrcm93KyciICcrKGxheWVyLmxvY2tlZD8nY2hlY2tlZCc6JycpKyc+" +
                "PC90ZD48dGQgY2xhc3M9Im1hcmstY29sIj48c2VsZWN0IGRhdGEtbGF5ZXItZmllbGQ9Im1hcmsiIGRhdGEtaWR4PSInK2lkeCsn" +
                "IiBkYXRhLXJvdz0iJytyb3crJyI+PG9wdGlvbiB2YWx1ZT0iIiAnKyghbGF5ZXIubWFyaz8nc2VsZWN0ZWQnOicnKSsnPuaXoOag" +
                "h+iusDwvb3B0aW9uPjxvcHRpb24gdmFsdWU9Iueuoee6v+WxgiIgJysobGF5ZXIubWFyaz09PSfnrqHnur/lsYInPydzZWxlY3Rl" +
                "ZCc6JycpKyc+566h57q/5bGCPC9vcHRpb24+PG9wdGlvbiB2YWx1ZT0i5LqV5LiL5bGCIiAnKyhsYXllci5tYXJrPT09J+S6leS4" +
                "i+Wxgic/J3NlbGVjdGVkJzonJykrJz7kupXkuIvlsYI8L29wdGlvbj48L3NlbGVjdD48L3RkPjx0ZCBjbGFzcz0icm93LXRvb2xz" +
                "Ij48ZGl2IGNsYXNzPSJpbmxpbmUtdG9vbHMiPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0iaWNvbi1idG4iIGRhdGEtbGF5" +
                "ZXItcm93LWFjdGlvbj0idXAiIGRhdGEtaWR4PSInK2lkeCsnIiBkYXRhLXJvdz0iJytyb3crJyI+4oaRPC9idXR0b24+PGJ1dHRv" +
                "biB0eXBlPSJidXR0b24iIGNsYXNzPSJpY29uLWJ0biIgZGF0YS1sYXllci1yb3ctYWN0aW9uPSJkb3duIiBkYXRhLWlkeD0iJytp" +
                "ZHgrJyIgZGF0YS1yb3c9Iicrcm93KyciPuKGkzwvYnV0dG9uPjxidXR0b24gdHlwZT0iYnV0dG9uIiBjbGFzcz0iaWNvbi1idG4g" +
                "ZGFuZ2VyIiBkYXRhLWxheWVyLXJvdy1hY3Rpb249ImRlbGV0ZSIgZGF0YS1pZHg9IicraWR4KyciIGRhdGEtcm93PSInK3Jvdysn" +
                "Ij7DlzwvYnV0dG9uPjwvZGl2PjwvdGQ+PC90cj4nO31odG1sKz0nPC90Ym9keT48L3RhYmxlPic7fWh0bWwrPSc8L2Rpdj48L2Rp" +
                "dj4nO3JldHVybiBodG1sO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBlLmNvbW1vbkxheWVyT3B0aW9ucz1mdW5j" +
                "dGlvbihwKXt2YXIgaXNOb2RlPVN0cmluZyhwLmtpbmR8fCcnKS5pbmRleE9mKCfkupUnKT49MDtyZXR1cm4gY29tbW9uTGF5ZXJz" +
                "Lm1hcChmdW5jdGlvbihsYXllcixpKXt2YXIgaXRlbT17bmFtZTpsYXllci5uYW1lLGhlaWdodDpsYXllci5oZWlnaHQsbG9ja2Vk" +
                "OmxheWVyLmxvY2tlZCxtYXJrOmxheWVyLm1hcmt9O2lmKGlzTm9kZSYmaXRlbS5tYXJrPT09J+euoee6v+WxgicmJml0ZW0ubmFt" +
                "ZS5pbmRleE9mKCflnqvlsYInKT49MClpdGVtLm1hcms9J+S6leS4i+Wxgic7cmV0dXJuICc8b3B0aW9uIHZhbHVlPSInK2krJyI+" +
                "Jytlc2MoaXRlbS5uYW1lKycgJytpdGVtLmhlaWdodCsoaXRlbS5sb2NrZWQ/JyDplIHlrponOicnKSsoaXRlbS5tYXJrPycgJytp" +
                "dGVtLm1hcms6JycpKSsnPC9vcHRpb24+Jzt9KS5qb2luKCcnKTt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5i" +
                "aW5kU3RydWN0dXJlRWRpdG9ycz1mdW5jdGlvbihwKXt2YXIgc2VsZj10aGlzO1tdLnNsaWNlLmNhbGwodGhpcy5lZGl0b3IucXVl" +
                "cnlTZWxlY3RvckFsbCgnW2RhdGEtbGF5ZXItcm93XScpKS5mb3JFYWNoKGZ1bmN0aW9uKHJvd0VsKXtyb3dFbC5hZGRFdmVudExp" +
                "c3RlbmVyKCdjbGljaycsZnVuY3Rpb24oKXt2YXIgaWR4PXBhcnNlSW50KHJvd0VsLmdldEF0dHJpYnV0ZSgnZGF0YS1pZHgnKSwx" +
                "MCk7dmFyIHJvdz1wYXJzZUludChyb3dFbC5nZXRBdHRyaWJ1dGUoJ2RhdGEtbGF5ZXItcm93JyksMTApO3NlbGYuc2VsZWN0ZWRM" +
                "YXllcltzZWxmLnByb2ZpbGVLZXkocCxpZHgpXT1yb3c7c2VsZi5yZW5kZXIoKTt9KTt9KTtbXS5zbGljZS5jYWxsKHRoaXMuZWRp" +
                "dG9yLnF1ZXJ5U2VsZWN0b3JBbGwoJ1tkYXRhLWxheWVyLWZpZWxkXScpKS5mb3JFYWNoKGZ1bmN0aW9uKGlucHV0KXtpbnB1dC5h" +
                "ZGRFdmVudExpc3RlbmVyKCdjbGljaycsZnVuY3Rpb24oZXYpe2V2LnN0b3BQcm9wYWdhdGlvbigpO30pO2Z1bmN0aW9uIHN5bmMo" +
                "KXt2YXIgaWR4PXBhcnNlSW50KGlucHV0LmdldEF0dHJpYnV0ZSgnZGF0YS1pZHgnKSwxMCk7dmFyIHJvdz1wYXJzZUludChpbnB1" +
                "dC5nZXRBdHRyaWJ1dGUoJ2RhdGEtcm93JyksMTApO3ZhciBmaWVsZD1pbnB1dC5nZXRBdHRyaWJ1dGUoJ2RhdGEtbGF5ZXItZmll" +
                "bGQnKTt2YXIgZj1wLmZpZWxkc1tpZHhdO3NlbGYuZW5zdXJlTGF5ZXJzKGYpO2lmKCFmLmxheWVyc1tyb3ddKXJldHVybjtpZihm" +
                "aWVsZD09PSdsb2NrZWQnKWYubGF5ZXJzW3Jvd10ubG9ja2VkPWlucHV0LmNoZWNrZWQ7ZWxzZSBmLmxheWVyc1tyb3ddW2ZpZWxk" +
                "XT1pbnB1dC52YWx1ZTtzZWxmLnN5bmNTdHJ1Y3R1cmVWYWx1ZShmKTtzZWxmLnVwZGF0ZVN0cnVjdHVyZVByZXZpZXcoaW5wdXQs" +
                "Zik7fWlucHV0LmFkZEV2ZW50TGlzdGVuZXIoJ2lucHV0JyxzeW5jKTtpbnB1dC5hZGRFdmVudExpc3RlbmVyKCdjaGFuZ2UnLHN5" +
                "bmMpO30pO1tdLnNsaWNlLmNhbGwodGhpcy5lZGl0b3IucXVlcnlTZWxlY3RvckFsbCgnW2RhdGEtbGF5ZXItYWN0aW9uXScpKS5m" +
                "b3JFYWNoKGZ1bmN0aW9uKGJ0bil7YnRuLmFkZEV2ZW50TGlzdGVuZXIoJ2NsaWNrJyxmdW5jdGlvbihldil7ZXYuc3RvcFByb3Bh" +
                "Z2F0aW9uKCk7c2VsZi5oYW5kbGVMYXllckFjdGlvbihwLHBhcnNlSW50KGJ0bi5nZXRBdHRyaWJ1dGUoJ2RhdGEtaWR4JyksMTAp" +
                "LGJ0bi5nZXRBdHRyaWJ1dGUoJ2RhdGEtbGF5ZXItYWN0aW9uJykpO30pO30pO1tdLnNsaWNlLmNhbGwodGhpcy5lZGl0b3IucXVl" +
                "cnlTZWxlY3RvckFsbCgnW2RhdGEtbGF5ZXItcm93LWFjdGlvbl0nKSkuZm9yRWFjaChmdW5jdGlvbihidG4pe2J0bi5hZGRFdmVu" +
                "dExpc3RlbmVyKCdjbGljaycsZnVuY3Rpb24oZXYpe2V2LnN0b3BQcm9wYWdhdGlvbigpO3ZhciBpZHg9cGFyc2VJbnQoYnRuLmdl" +
                "dEF0dHJpYnV0ZSgnZGF0YS1pZHgnKSwxMCk7c2VsZi5zZWxlY3RlZExheWVyW3NlbGYucHJvZmlsZUtleShwLGlkeCldPXBhcnNl" +
                "SW50KGJ0bi5nZXRBdHRyaWJ1dGUoJ2RhdGEtcm93JyksMTApO3NlbGYuaGFuZGxlTGF5ZXJBY3Rpb24ocCxpZHgsYnRuLmdldEF0" +
                "dHJpYnV0ZSgnZGF0YS1sYXllci1yb3ctYWN0aW9uJykpO30pO30pO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBl" +
                "LnVwZGF0ZVN0cnVjdHVyZVByZXZpZXc9ZnVuY3Rpb24oaW5wdXQsZil7fTsKICBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5" +
                "cGUuaGFuZGxlTGF5ZXJBY3Rpb249ZnVuY3Rpb24ocCxpZHgsYWN0aW9uKXt2YXIgZj1wLmZpZWxkc1tpZHhdO3RoaXMuZW5zdXJl" +
                "TGF5ZXJzKGYpO3ZhciBrZXk9dGhpcy5wcm9maWxlS2V5KHAsaWR4KTt2YXIgc2VsZWN0ZWQ9dGhpcy5zZWxlY3RlZExheWVyW2tl" +
                "eV09PW51bGw/KGYubGF5ZXJzLmxlbmd0aD8wOi0xKTp0aGlzLnNlbGVjdGVkTGF5ZXJba2V5XTtpZihhY3Rpb249PT0nYWRkJyl7" +
                "Zi5sYXllcnMucHVzaCh7bmFtZTon5paw57uT5p6E5bGCJyxoZWlnaHQ6JzAuMTAnLGxvY2tlZDpmYWxzZSxtYXJrOicnfSk7dGhp" +
                "cy5zZWxlY3RlZExheWVyW2tleV09Zi5sYXllcnMubGVuZ3RoLTE7fWVsc2UgaWYoYWN0aW9uPT09J3Jlc3RvcmUnKXt2YXIgYnVp" +
                "bHRJblByb2ZpbGU9dGhpcy5maW5kQnVpbHRJblByb2ZpbGUocCk7dmFyIGJ1aWx0SW5GaWVsZD10aGlzLmZpbmRGaWVsZChidWls" +
                "dEluUHJvZmlsZSwnQmFja2ZpbGxTdHJ1Y3R1cmUnKTtmLmxheWVycz10aGlzLnBhcnNlU3RydWN0dXJlKGJ1aWx0SW5GaWVsZD9i" +
                "dWlsdEluRmllbGQudmFsdWU6JycpO3RoaXMuc2VsZWN0ZWRMYXllcltrZXldPWYubGF5ZXJzLmxlbmd0aD8wOi0xO3RoaXMudG9h" +
                "c3QoJ+W3suaBouWkjeW9k+WJjeihqOeahOWGhee9rue7k+aehOWxgu+8jOeCueWHu+S/neWtmOWQjueUn+aViCcsJ2luZm8nKTt9" +
                "ZWxzZSBpZihzZWxlY3RlZD49MCYmc2VsZWN0ZWQ8Zi5sYXllcnMubGVuZ3RoKXtpZihhY3Rpb249PT0nZGVsZXRlJyl7Zi5sYXll" +
                "cnMuc3BsaWNlKHNlbGVjdGVkLDEpO3RoaXMuc2VsZWN0ZWRMYXllcltrZXldPU1hdGgubWluKHNlbGVjdGVkLGYubGF5ZXJzLmxl" +
                "bmd0aC0xKTt9ZWxzZSBpZihhY3Rpb249PT0ndXAnJiZzZWxlY3RlZD4wKXt2YXIgYT1mLmxheWVycy5zcGxpY2Uoc2VsZWN0ZWQs" +
                "MSlbMF07Zi5sYXllcnMuc3BsaWNlKHNlbGVjdGVkLTEsMCxhKTt0aGlzLnNlbGVjdGVkTGF5ZXJba2V5XT1zZWxlY3RlZC0xO31l" +
                "bHNlIGlmKGFjdGlvbj09PSdkb3duJyYmc2VsZWN0ZWQ8Zi5sYXllcnMubGVuZ3RoLTEpe3ZhciBiPWYubGF5ZXJzLnNwbGljZShz" +
                "ZWxlY3RlZCwxKVswXTtmLmxheWVycy5zcGxpY2Uoc2VsZWN0ZWQrMSwwLGIpO3RoaXMuc2VsZWN0ZWRMYXllcltrZXldPXNlbGVj" +
                "dGVkKzE7fX10aGlzLnN5bmNTdHJ1Y3R1cmVWYWx1ZShmKTt0aGlzLnJlbmRlcigpO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2Uu" +
                "cHJvdG90eXBlLnBhcnNlU3RydWN0dXJlPWZ1bmN0aW9uKHRleHQpe3ZhciBsaW5lcz1TdHJpbmcodGV4dHx8JycpLnJlcGxhY2Uo" +
                "L++8my9nLCdcbicpLnJlcGxhY2UoLzsvZywnXG4nKS5yZXBsYWNlKC/vvIwvZywnXG4nKS5yZXBsYWNlKC8sL2csJ1xuJykucmVw" +
                "bGFjZSgvXHJcbi9nLCdcbicpLnJlcGxhY2UoL1xyL2csJ1xuJykuc3BsaXQoJ1xuJyk7dmFyIHJlc3VsdD1bXTtsaW5lcy5mb3JF" +
                "YWNoKGZ1bmN0aW9uKHJhdyl7dmFyIGxpbmU9U3RyaW5nKHJhd3x8JycpLnRyaW0oKTtpZighbGluZSlyZXR1cm47dmFyIGxvY2tl" +
                "ZD0v6ZSB5a6afOWbuuWumi9pLnRlc3QobGluZSk7dmFyIG1hcms9L+S6leS4i+WxgnzkupXkuIvmlrnlnqvlsYIvaS50ZXN0KGxp" +
                "bmUpPyfkupXkuIvlsYInOigv566h57q/5bGCfOeuoemBk+WxgnznrqHlsYIvaS50ZXN0KGxpbmUpPyfnrqHnur/lsYInOicnKTt2" +
                "YXIgbWF0Y2hlcz1saW5lLm1hdGNoKC9bLStdP1xkKyg/OlwuXGQrKT8vZyk7dmFyIGhlaWdodD1tYXRjaGVzJiZtYXRjaGVzLmxl" +
                "bmd0aD9tYXRjaGVzW21hdGNoZXMubGVuZ3RoLTFdOicnO3ZhciBuYW1lPWxpbmU7aWYoaGVpZ2h0KXt2YXIgcG9zPWxpbmUubGFz" +
                "dEluZGV4T2YoaGVpZ2h0KTtuYW1lPWxpbmUuc3Vic3RyaW5nKDAscG9zKTt9bmFtZT1uYW1lLnJlcGxhY2UoL+mUgeWumnzlm7rl" +
                "rpp8566h57q/5bGCfOeuoemBk+WxgnznrqHlsYJ85LqV5LiL5bGCfOS6leS4i+aWueWeq+Wxgi9pZywnJykudHJpbSgpO2lmKCFu" +
                "YW1lKW5hbWU9bGluZS5yZXBsYWNlKC/plIHlrpp85Zu65a6afOeuoee6v+WxgnznrqHpgZPlsYJ8566h5bGCfOS6leS4i+Wxgnzk" +
                "upXkuIvmlrnlnqvlsYIvaWcsJycpLnJlcGxhY2UoaGVpZ2h0LCcnKS50cmltKCk7cmVzdWx0LnB1c2goe25hbWU6bmFtZXx8J+e7" +
                "k+aehOWxgicsaGVpZ2h0OmhlaWdodCxsb2NrZWQ6bG9ja2VkLG1hcms6bWFya30pO30pO3JldHVybiByZXN1bHQ7fTsKICBRdWFu" +
                "dGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUuYnVpbGRTdHJ1Y3R1cmVUZXh0PWZ1bmN0aW9uKGxheWVycyl7cmV0dXJuIChsYXll" +
                "cnN8fFtdKS5tYXAoZnVuY3Rpb24obGF5ZXIpe3ZhciBwYXJ0cz1bXTt2YXIgbmFtZT1TdHJpbmcobGF5ZXIubmFtZXx8JycpLnRy" +
                "aW0oKTt2YXIgaGVpZ2h0PVN0cmluZyhsYXllci5oZWlnaHR8fCcnKS50cmltKCk7aWYoIW5hbWUmJiFoZWlnaHQpcmV0dXJuICcn" +
                "O3BhcnRzLnB1c2gobmFtZXx8J+e7k+aehOWxgicpO2lmKGhlaWdodClwYXJ0cy5wdXNoKGhlaWdodCk7aWYobGF5ZXIubG9ja2Vk" +
                "KXBhcnRzLnB1c2goJ+mUgeWumicpO2lmKGxheWVyLm1hcmspcGFydHMucHVzaChsYXllci5tYXJrKTtyZXR1cm4gcGFydHMuam9p" +
                "bignICcpO30pLmZpbHRlcihmdW5jdGlvbihsaW5lKXtyZXR1cm4gISFsaW5lO30pLmpvaW4oJ1xuJyk7fTsKICBRdWFudGl0eURl" +
                "ZmF1bHRzUGFnZS5wcm90b3R5cGUuc3luY1N0cnVjdHVyZVZhbHVlPWZ1bmN0aW9uKGYpe3RoaXMuZW5zdXJlTGF5ZXJzKGYpO2Yu" +
                "dmFsdWU9dGhpcy5idWlsZFN0cnVjdHVyZVRleHQoZi5sYXllcnMpO307CiAgUXVhbnRpdHlEZWZhdWx0c1BhZ2UucHJvdG90eXBl" +
                "LmZpbmRGaWVsZD1mdW5jdGlvbihwcm9maWxlLGtleSl7aWYoIXByb2ZpbGUpcmV0dXJuIG51bGw7a2V5PVN0cmluZyhrZXl8fCcn" +
                "KS50b0xvd2VyQ2FzZSgpO2Zvcih2YXIgaT0wO2k8KHByb2ZpbGUuZmllbGRzfHxbXSkubGVuZ3RoO2krKyl7aWYoU3RyaW5nKHBy" +
                "b2ZpbGUuZmllbGRzW2ldLmtleXx8JycpLnRvTG93ZXJDYXNlKCk9PT1rZXkpcmV0dXJuIHByb2ZpbGUuZmllbGRzW2ldO31yZXR1" +
                "cm4gbnVsbDt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5maW5kQnVpbHRJblByb2ZpbGU9ZnVuY3Rpb24ocHJv" +
                "ZmlsZSl7Zm9yKHZhciBpPTA7aTx0aGlzLmJ1aWx0SW5Qcm9maWxlcy5sZW5ndGg7aSsrKXtpZih0aGlzLmJ1aWx0SW5Qcm9maWxl" +
                "c1tpXS5pZD09PXByb2ZpbGUuaWR8fHRoaXMuYnVpbHRJblByb2ZpbGVzW2ldLmtpbmQ9PT1wcm9maWxlLmtpbmQpcmV0dXJuIHRo" +
                "aXMuYnVpbHRJblByb2ZpbGVzW2ldO31yZXR1cm4gbnVsbDt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5idWls" +
                "ZFBheWxvYWQ9ZnVuY3Rpb24oKXt2YXIgc2VsZj10aGlzO3ZhciBsaW5lcz1bXTt0aGlzLnByb2ZpbGVzLmZvckVhY2goZnVuY3Rp" +
                "b24ocCl7KHAuZmllbGRzfHxbXSkuZm9yRWFjaChmdW5jdGlvbihmKXtpZihzZWxmLmlzU3RydWN0dXJlRmllbGQoZikpc2VsZi5z" +
                "eW5jU3RydWN0dXJlVmFsdWUoZik7bGluZXMucHVzaChbcC5raW5kLGYua2V5LGYudHlwZSxmLnZhbHVlfHwnJ10ubWFwKGVuY29k" +
                "ZTY0KS5qb2luKCdcdCcpKTt9KTt9KTtyZXR1cm4gbGluZXMuam9pbignXG4nKTt9OwogIFF1YW50aXR5RGVmYXVsdHNQYWdlLnBy" +
                "b3RvdHlwZS5yZXN0b3JlQnVpbHRJbj1mdW5jdGlvbigpe2lmKCFjb25maXJtKCfmgaLlpI3lhoXnva7lsZ7mgKfpu5jorqTooajk" +
                "vJropobnm5blvZPliY3pobXpnaLnvJbovpHlhoXlrrnvvIzkv53lrZjliY3kuI3kvJrlhpnlhaXmlofku7bjgILmmK/lkKbnu6fn" +
                "u63vvJ8nKSlyZXR1cm47dGhpcy5wcm9maWxlcz1jbG9uZVByb2ZpbGVzKHRoaXMuYnVpbHRJblByb2ZpbGVzKTt0aGlzLnNlbGVj" +
                "dGVkTGF5ZXI9e307dGhpcy5hY3RpdmVJbmRleD0wO3RoaXMucmVuZGVyKCk7dGhpcy50b2FzdCgn5bey5oGi5aSN5Li65YaF572u" +
                "6buY6K6k6KGo77yM54K55Ye75L+d5a2Y5ZCO55Sf5pWIJywnaW5mbycpO307CiAgdy5DREJveFF1YW50aXR5RGVmYXVsdHNQYWdl" +
                "PXtjcmVhdGU6ZnVuY3Rpb24ob3B0aW9ucyl7cmV0dXJuIG5ldyBRdWFudGl0eURlZmF1bHRzUGFnZShvcHRpb25zKTt9LGNsb25l" +
                "UHJvZmlsZXM6Y2xvbmVQcm9maWxlcyxTdHJ1Y3R1cmVMYXllckVkaXRvcjp7cGFyc2U6ZnVuY3Rpb24odGV4dCl7cmV0dXJuIFF1" +
                "YW50aXR5RGVmYXVsdHNQYWdlLnByb3RvdHlwZS5wYXJzZVN0cnVjdHVyZSh0ZXh0KTt9LGJ1aWxkVGV4dDpmdW5jdGlvbihsYXll" +
                "cnMpe3JldHVybiBRdWFudGl0eURlZmF1bHRzUGFnZS5wcm90b3R5cGUuYnVpbGRTdHJ1Y3R1cmVUZXh0KGxheWVycyk7fX19Owp9" +
                "KSh3aW5kb3cpOwoKICB0cnl7CiAgICB2YXIgcGFnZT13aW5kb3cuQ0RCb3hRdWFudGl0eURlZmF1bHRzUGFnZS5jcmVhdGUoe3Jv" +
                "b3RJZDonZGVmYXVsdFByb2ZpbGVzUGFnZScsdGFic0lkOidkZWZhdWx0UHJvZmlsZVRhYnMnLGVkaXRvcklkOidkZWZhdWx0UHJv" +
                "ZmlsZUVkaXRvcicsZGF0YTpkZWZhdWx0UHJvZmlsZXNEYXRhLGJ1aWx0SW5EYXRhOmJ1aWx0SW5EZWZhdWx0UHJvZmlsZXNEYXRh" +
                "LHRvYXN0OnRvYXN0fSk7CiAgICBwYWdlLnJlbmRlcigpOwogICAgZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ3NhdmVEZWZhdWx0" +
                "UHJvZmlsZXNCdXR0b24nKS5hZGRFdmVudExpc3RlbmVyKCdjbGljaycsZnVuY3Rpb24oKXtwb3N0KCdzYXZlRGVmYXVsdFByb2Zp" +
                "bGVzJyxwYWdlLmJ1aWxkUGF5bG9hZCgpKTt9KTsKICAgIGRvY3VtZW50LmdldEVsZW1lbnRCeUlkKCdyZXN0b3JlRGVmYXVsdFBy" +
                "b2ZpbGVzQnV0dG9uJykuYWRkRXZlbnRMaXN0ZW5lcignY2xpY2snLGZ1bmN0aW9uKCl7cGFnZS5yZXN0b3JlQnVpbHRJbigpO30p" +
                "OwogICAgZG9jdW1lbnQuZ2V0RWxlbWVudEJ5SWQoJ29wZW5MZWdhY3lEZWZhdWx0UHJvZmlsZXMnKS5hZGRFdmVudExpc3RlbmVy" +
                "KCdjbGljaycsZnVuY3Rpb24oKXtwb3N0KCdvcGVuTGVnYWN5RGVmYXVsdFByb2ZpbGVzJywnJyk7fSk7CiAgICBkb2N1bWVudC5n" +
                "ZXRFbGVtZW50QnlJZCgnY2xvc2VXaW5kb3cnKS5hZGRFdmVudExpc3RlbmVyKCdjbGljaycsZnVuY3Rpb24oKXtwb3N0KCdjbG9z" +
                "ZScsJycpO30pOwogICAgc2V0VGltZW91dChmdW5jdGlvbigpe3Bvc3QoJ3JlYWR5JywncXVhbnRpdHktZGVmYXVsdHMnKTt9LDgw" +
                "KTsKICB9Y2F0Y2goZXgpe3ZhciBtZXNzYWdlPShleCYmZXguc3RhY2spfHxTdHJpbmcoZXh8fCfmnKrnn6XplJnor68nKTtzaG93" +
                "RXJyb3IoJ+WxnuaAp+m7mOiupOihqOmhtemdouWIneWni+WMluWksei0pScsbWVzc2FnZSk7cG9zdCgncGFnZUVycm9yJyxtZXNz" +
                "YWdlKTt9Cn0pKCk7Cg==";

        public static string BuildEmbeddedSection()
        {
            var page = new StringBuilder();
            page.Append("<section id=\"defaultProfilesPage\" class=\"quantity-defaults-page quantity-defaults-embedded\" style=\"display:none\">");
            page.Append("<article class=\"setting-card profiles-card qd-card\" data-quantity-defaults=\"1\">");
            page.Append("<div class=\"qd-tabs-row\"><div id=\"defaultProfileTabs\" class=\"profile-tabs qd-tabs\"></div><button id=\"saveDefaultProfilesButton\" class=\"primary-btn\">保存</button></div><div id=\"defaultProfileEditor\" class=\"profile-editor qd-editor\"></div>");
            page.Append("</article></section>");
            return page.ToString();
        }

        public static string BuildStandaloneDocument(CDBoxStudioSettings settings, string logFilePath)
        {
            settings = settings ?? new CDBoxStudioSettings();
            settings.Normalize();

            var html = new StringBuilder();
            html.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>CDBox Studio - 属性默认表</title><style>");
            html.Append(BuildStyles(true));
            html.Append("</style></head><body data-theme=\"").Append(HtmlAttr(settings.Theme)).Append("\" class=\"").Append(settings.AnimationsEnabled ? string.Empty : "no-animations").Append("\">");
            html.Append("<section id=\"defaultProfilesPage\" class=\"quantity-defaults-page quantity-defaults-standalone\" data-route=\"quantity-defaults\">");
            html.Append("<main class=\"qd-page\"><article class=\"qd-card\"><div class=\"qd-tabs-row\"><div id=\"defaultProfileTabs\" class=\"qd-tabs\"></div><button id=\"saveDefaultProfilesButton\" class=\"qd-btn primary\">保存</button></div>");
            html.Append("<div id=\"defaultProfileEditor\" class=\"qd-editor\"></div></article></main></section><div id=\"toastStack\" class=\"toast-stack\"></div>");
            html.Append("<script>\n");
            html.Append("var defaultProfilesData=").Append(CDBoxStudioDefaultProfiles.BuildDefaultsJson(CDBoxStudioDefaultProfiles.LoadDefaults())).Append(";\n");
            html.Append("var builtInDefaultProfilesData=").Append(CDBoxStudioDefaultProfiles.BuildDefaultsJson(CDBoxStudioDefaultProfiles.LoadBuiltInDefaults())).Append(";\n");
            html.Append(BuildStandaloneBootstrapScript());
            html.Append("</script></body></html>");
            return html.ToString();
        }

        public static string BuildStyles(bool standalone)
        {
            return Decode(standalone ? StandaloneStyleBase64 : EmbeddedStyleBase64)
                + ".layer-drag-handle{display:inline-flex;align-items:center;gap:4px;cursor:grab;user-select:none;color:var(--muted);font-weight:800}.layer-drag-handle:active{cursor:grabbing;color:var(--brand)}.layer-table tr.dragging{opacity:.4}.layer-table tr.drag-target td{background:rgba(59,130,246,.10)}.qd-tabs-row{display:flex;align-items:flex-start;gap:12px;padding-bottom:12px;margin-bottom:14px;border-bottom:1px solid var(--line)}.qd-tabs-row .qd-tabs{flex:1;margin-bottom:0;padding-bottom:0;border-bottom:0;flex-wrap:wrap}.qd-tabs-row #saveDefaultProfilesButton{flex:0 0 auto;margin-left:auto}.quantity-defaults-page .qd-card{border-radius:12px}.quantity-defaults-page .qd-field{border-radius:10px}";
        }

        public static string BuildEmbeddedBridgeScript()
        {
            return NormalizeTableExperience(Decode(EmbeddedBridgeScriptBase64));
        }

        private static string BuildStandaloneBootstrapScript()
        {
            return NormalizeTableExperience(Decode(StandaloneBootstrapScriptBase64));
        }

        private static string NormalizeTableExperience(string script)
        {
            script = script.Replace("+(f.help?'<p>'+esc(f.help)+'</p>':'')", string.Empty);
            script = script.Replace("<span>每层填写层名、厚度、锁定和管线层/井下层标记；保存时自动拼回旧版多行结构层文本。</span>", string.Empty);
            script = script.Replace("<button type=\"button\" class=\"small-btn\" data-layer-action=\"up\" data-idx=\"'+idx+'\">上移</button>", string.Empty);
            script = script.Replace("<button type=\"button\" class=\"small-btn\" data-layer-action=\"down\" data-idx=\"'+idx+'\">下移</button>", string.Empty);
            script = script.Replace("<button type=\"button\" class=\"icon-btn\" data-layer-row-action=\"up\" data-idx=\"'+idx+'\" data-row=\"'+row+'\">↑</button>", string.Empty);
            script = script.Replace("<button type=\"button\" class=\"icon-btn\" data-layer-row-action=\"down\" data-idx=\"'+idx+'\" data-row=\"'+row+'\">↓</button>", string.Empty);
            script = script.Replace("<button type=\"button\" class=\"small-btn warning\" data-layer-action=\"restore\" data-idx=\"'+idx+'\">恢复此表默认结构层</button>", string.Empty);
            script = script.Replace("document.getElementById('openLegacyDefaultProfiles').addEventListener('click',function(){post('openLegacyDefaultProfiles','');});", string.Empty);
            script = script.Replace("document.getElementById('restoreDefaultProfilesButton').addEventListener('click',function(){page.restoreBuiltIn();});", string.Empty);
            script = script.Replace("document.getElementById('closeWindow').addEventListener('click',function(){post('close','');});", string.Empty);
            script = script.Replace("<td class=\"priority\">'+(row+1)+'</td>", "<td class=\"priority\"><span class=\"layer-drag-handle\" draggable=\"true\" title=\"拖拽排序\">⋮⋮ '+(row+1)+'</span></td>");
            script = script.Replace(
                "QuantityDefaultsPage.prototype.bindStructureEditors=function(p){var self=this;",
                "QuantityDefaultsPage.prototype.bindStructureEditors=function(p){var self=this,draggedLayer=null;[].slice.call(this.editor.querySelectorAll('[data-layer-row]')).forEach(function(rowEl){var handle=rowEl.querySelector('.layer-drag-handle');if(handle){handle.addEventListener('click',function(ev){ev.stopPropagation();});handle.addEventListener('dragstart',function(ev){draggedLayer=rowEl;rowEl.classList.add('dragging');if(ev.dataTransfer)ev.dataTransfer.effectAllowed='move';});handle.addEventListener('dragend',function(){draggedLayer=null;rowEl.classList.remove('dragging','drag-target');});}rowEl.addEventListener('dragover',function(ev){if(!draggedLayer||draggedLayer===rowEl)return;ev.preventDefault();rowEl.classList.add('drag-target');});rowEl.addEventListener('dragleave',function(){rowEl.classList.remove('drag-target');});rowEl.addEventListener('drop',function(ev){if(!draggedLayer||draggedLayer===rowEl)return;ev.preventDefault();var idx=parseInt(rowEl.getAttribute('data-idx'),10),from=parseInt(draggedLayer.getAttribute('data-layer-row'),10),to=parseInt(rowEl.getAttribute('data-layer-row'),10),f=p.fields[idx],box=rowEl.getBoundingClientRect();self.ensureLayers(f);var item=f.layers.splice(from,1)[0];if(from<to)to--;if(ev.clientY>box.top+box.height/2)to++;to=Math.max(0,Math.min(f.layers.length,to));f.layers.splice(to,0,item);self.selectedLayer[self.profileKey(p,idx)]=to;self.syncStructureValue(f);draggedLayer=null;self.render();});});");
            script = script.Replace("else if(action==='up'&&selected>0){var a=f.layers.splice(selected,1)[0];f.layers.splice(selected-1,0,a);this.selectedLayer[key]=selected-1;}else if(action==='down'&&selected<f.layers.length-1){var b=f.layers.splice(selected,1)[0];f.layers.splice(selected+1,0,b);this.selectedLayer[key]=selected+1;}", string.Empty);
            return script;
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static string Html(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static string HtmlAttr(string text)
        {
            return Html(text).Replace("\r", string.Empty).Replace("\n", " ");
        }
    }
}
