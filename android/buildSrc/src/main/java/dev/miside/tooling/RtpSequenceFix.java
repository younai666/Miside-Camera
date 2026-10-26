package dev.miside.tooling;

import com.android.build.api.instrumentation.AsmClassVisitorFactory;
import com.android.build.api.instrumentation.ClassContext;
import com.android.build.api.instrumentation.ClassData;
import com.android.build.api.instrumentation.InstrumentationParameters;
import org.objectweb.asm.ClassVisitor;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/** Media3 1.8.0 uses 65535 instead of the 65536-value RTP sequence space. */
public abstract class RtpSequenceFix implements AsmClassVisitorFactory<InstrumentationParameters.None> {
    @Override public boolean isInstrumentable(ClassData data) {
        return data.getClassName().equals("androidx.media3.exoplayer.rtsp.RtpPacketReorderingQueue");
    }

    @Override public ClassVisitor createClassVisitor(ClassContext context, ClassVisitor next) {
        return new ClassVisitor(Opcodes.ASM9, next) {
            private int fixes;
            @Override public MethodVisitor visitMethod(int access, String name, String descriptor, String signature, String[] exceptions) {
                MethodVisitor method = super.visitMethod(access, name, descriptor, signature, exceptions);
                if (!name.equals("calculateSequenceNumberShift") || !descriptor.equals("(II)I")) return method;
                return new MethodVisitor(Opcodes.ASM9, method) {
                    @Override public void visitLdcInsn(Object value) {
                        if (Integer.valueOf(65535).equals(value)) { fixes++; value = 65536; }
                        super.visitLdcInsn(value);
                    }
                };
            }
            @Override public void visitEnd() {
                // A dependency update must review/remove this patch, never silently skip it.
                if (fixes != 1) throw new IllegalStateException("Review the Media3 RTP rollover fix: expected one 65535 constant, got " + fixes);
                super.visitEnd();
            }
        };
    }
}
