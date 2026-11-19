// Admission only: no screen model, coordinate calculation, or reply synthesis.
export class Admission {
  constructor() { this.decoder = new TextDecoder('utf-8', {fatal:true,ignoreBOM:true}); this.pending=''; this.byteTail=Buffer.alloc(0); this.failed=false; }
  feed(bytes, final=false) {
    if(this.failed) throw Error('Admission already failed');
    try {
      const raw=Buffer.concat([this.byteTail,Buffer.from(bytes)]);
      let end=raw.length;
      if(!final&&end) {
        let start=end-1;while(start>=0&&(raw[start]&0xc0)===0x80)start--;
        if(start>=0) {
          const first=raw[start];const need=first>=0xf0&&first<=0xf4?4:first>=0xe0&&first<=0xef?3:first>=0xc2&&first<=0xdf?2:1;
          if(end-start<need)end=start;
        }
      }
      this.byteTail=raw.subarray(end);
      this.pending += this.decoder.decode(raw.subarray(0,end));
      let i=0, queries=0;
      while(i<this.pending.length) {
        const ch=this.pending[i];
        if(ch==='\x1b') {
          if(i+1===this.pending.length) break;
          if('=>'.includes(this.pending[i+1])) { i+=2;continue; }
          if(this.pending[i+1]!=='[') throw Error('Unadmitted escape');
          const tail=this.pending.slice(i);
          const m=/^\x1b\[([0-9;?]*)([@-~])/.exec(tail);
          if(!m) {
            if(/^\x1b\[[0-9;?]*$/.test(tail)&&tail.length<64) break;
            throw Error('Unadmitted CSI');
          }
          const [seq,args,end]=m;
          const vals=args.split(';').map(x=>Number(x||0));
          let ok=false;
          if(end==='h'||end==='l') ok=args==='?1'||args==='?25';
          else if(end==='m') ok=vals.length<=16&&vals.every(x=>[0,1,2,3,4,9,22,23,24,29].includes(x));
          else if(end==='n') {ok=args==='6';if(ok)queries++;}
          else if('ABCDG'.includes(end)) ok=/^\d*$/.test(args)&&vals[0]<=100;
          else if(end==='H'||end==='f') ok=/^\d*(;\d*)?$/.test(args)&&vals.every(x=>x<=100);
          else if(end==='K') ok=/^[012]?$/.test(args);
          else if(end==='J') ok=args===''||args==='0'||args==='2'||args==='3';
          else if(end==='s'||end==='u') ok=args===''; // Actual cooperative draft save/restore cursor.
          if(!ok) throw Error('Unadmitted CSI '+JSON.stringify(seq));
          i+=seq.length;continue;
        }
        const cp=this.pending.codePointAt(i);
        if(cp<32&&!['\r','\n','\b','\t'].includes(ch)||cp===127||cp>=128&&cp<=159) throw Error('Unadmitted control');
        i+=cp>65535?2:1;
      }
      const text=this.pending.slice(0,i);this.pending=this.pending.slice(i);
      if(final&&this.pending) throw Error('Partial control at EOF');
      return {bytes:Buffer.from(text,'utf8'),queries,pending:this.pending.length>0||this.byteTail.length>0};
    } catch(e) { this.failed=true;throw e; }
  }
}
