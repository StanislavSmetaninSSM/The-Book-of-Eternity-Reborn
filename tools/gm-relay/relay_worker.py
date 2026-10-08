"""Credential-free local inspect/answer/close entrypoint; it never generates a response."""
import argparse
import json
from pathlib import Path
import sys

from relay_contract import RelayError, publish_response, read_bounded, read_request, request_close


def output(value):
    # The worker is a JSON protocol even when stdout is a Windows pipe whose
    # default text encoding cannot represent the retained Unicode prompt.
    # JSON escapes keep Unicode exact for consumers using legacy pipe codepages;
    # packet/prompt files retain their original bytes, with no escaping rewrite.
    sys.stdout.buffer.write((json.dumps(value, ensure_ascii=True) + '\n').encode('utf8'))
    sys.stdout.buffer.flush()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('inspect').add_argument('request', type=Path)
    answer = commands.add_parser('answer')
    answer.add_argument('request', type=Path)
    answer.add_argument('packet', type=Path)
    answer.add_argument('--adapter', required=True)
    commands.add_parser('close').add_argument('queue', type=Path)
    args = parser.parse_args()
    try:
        if args.command == 'close':
            request_close(args.queue)
            output(dict(CloseRequested=True, ClosedObserved=False))
        else:
            directory = args.request.resolve()
            request = read_request(directory.parent, directory)
            if args.command == 'inspect':
                header = json.loads(request.header_bytes)
                output(dict(Request=header, Prompt=request.prompt.decode('utf8'),
                            GameRequest=request.game_request.decode('utf8')))
            else:
                publish_response(request, read_bounded(args.packet), adapter_id=args.adapter)
                output(dict(Published=True, Executed=False, Accepted=False))
        return 0
    except (RelayError, OSError, UnicodeError) as ex:
        print(type(ex).__name__ + ': ' + str(ex), file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
